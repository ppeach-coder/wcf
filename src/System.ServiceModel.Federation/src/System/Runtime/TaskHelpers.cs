﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Threading;
using System.Threading.Tasks;

namespace System.ServiceModel.Federation.System.Runtime
{
    internal static class TaskHelpers
    {
        // Helper method when implementing an APM wrapper around a Task based async method which returns a result. 
        // In the BeginMethod method, you would call use ToApm to wrap a call to MethodAsync:
        //     return MethodAsync(params).ToApm(callback, state);
        // In the EndMethod, you would use ToApmEnd to ensure the correct exception handling
        // This will handle throwing exceptions in the correct place and ensure the IAsyncResult contains the provided
        // state object
        public static Task ToApm(this Task task, AsyncCallback callback, object state)
        {
            // When using APM, the returned IAsyncResult must have the passed in state object stored in AsyncState. This
            // is so the callback can regain state. If the incoming task already holds the state object, there's no need
            // to create a TaskCompletionSource to ensure the returned (IAsyncResult)Task has the right state object.
            // This is a performance optimization for this special case.
            if (task.AsyncState == state)
            {
                if (callback != null)
                {
                    task.ContinueWith((antecedent, obj) =>
                    {
                        var callbackObj = obj as AsyncCallback;
                        callbackObj(antecedent);
                    }, callback, CancellationToken.None, TaskContinuationOptions.HideScheduler, TaskScheduler.Default);
                }
                return task;
            }

            // Need to create a TaskCompletionSource so that the returned Task object has the correct AsyncState value.
            // As we intend to create a task with no Result value, we don't care what result type the TCS holds as we
            // won't be using it. As Task<TResult> derives from Task, the returned Task is compatible.
            var tcs = new TaskCompletionSource<object>(state);
            var continuationState = Tuple.Create(tcs, callback);
            task.ContinueWith((antecedent, obj) =>
            {
                var tuple = obj as Tuple<TaskCompletionSource<object>, AsyncCallback>;
                var tcsObj = tuple.Item1;
                var callbackObj = tuple.Item2;
                if (antecedent.IsFaulted)
                {
                    tcsObj.TrySetException(antecedent.Exception.InnerException);
                }
                else if (antecedent.IsCanceled)
                {
                    tcsObj.TrySetCanceled();
                }
                else
                {
                    tcsObj.TrySetResult(null);
                }

                if (callbackObj != null)
                {
                    callbackObj(tcsObj.Task);
                }
            }, continuationState, CancellationToken.None, TaskContinuationOptions.HideScheduler, TaskScheduler.Default);
            return tcs.Task;
        }

        // Helper method to implement the End method of an APM method pair which is wrapping a Task based
        // async method when the Task does not return result.
        public static void ToApmEnd(this IAsyncResult iar)
        {
            Task task = iar as Task;
            task.GetAwaiter().GetResult();
        }

        // Helper method to implement the End method of an APM method pair which is wrapping a Task based
        // async method when the Task returns a result. By using task.GetAwaiter.GetResult(), the exception
        // handling conventions are the same as when await'ing a task, i.e. this throws the first exception
        // and doesn't wrap it in an AggregateException. It also throws the right exception if the task was
        // cancelled.
        public static TResult ToApmEnd<TResult>(this IAsyncResult iar)
        {
            Task<TResult> task = iar as Task<TResult>;
            return task.GetAwaiter().GetResult();
        }
    }
}
