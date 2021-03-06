﻿using FeatureFlowFramework.Helpers;
using FeatureFlowFramework.Helpers.Time;
using System;
using System.Threading.Tasks;

namespace FeatureFlowFramework.DataFlows.RPC
{
    public partial class RpcCaller
    {
        private interface IResponseHandler
        {
            bool Handle<M>(in M message);

            TimeFrame LifeTime { get; }

            void Cancel();
        }

        private class ResponseHandler<R> : IResponseHandler
        {
            private readonly long requestId;
            private readonly TaskCompletionSource<R> taskCompletionSource;
            public readonly TimeFrame lifeTime;

            public TimeFrame LifeTime => lifeTime;

            public ResponseHandler(long requestId, TaskCompletionSource<R> taskCompletionSource, TimeSpan timeout)
            {
                this.taskCompletionSource = taskCompletionSource;
                lifeTime = new TimeFrame(timeout);
                this.requestId = requestId;
            }

            public bool Handle<M>(in M message)
            {
                if(message is RpcResponse<R> myResponse && myResponse.RequestId == this.requestId)
                {
                    taskCompletionSource.SetResult(myResponse.Result);
                    return true;
                }
                else return false;
            }

            public void Cancel()
            {
                taskCompletionSource.SetCanceled();
            }
        }
    }
}