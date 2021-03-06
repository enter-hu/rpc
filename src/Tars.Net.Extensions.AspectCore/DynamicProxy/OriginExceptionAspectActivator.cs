﻿using AspectCore.Core.Utils;
using AspectCore.DynamicProxy;
using System;
using System.Threading.Tasks;

namespace Tars.Net.Extensions.AspectCore.DynamicProxy
{
    [NonAspect]
    public sealed class OriginExceptionAspectActivator : IAspectActivator
    {
        private readonly IAspectContextFactory aspectContextFactory;
        private readonly IAspectBuilderFactory aspectBuilderFactory;

        public OriginExceptionAspectActivator(IAspectContextFactory aspectContextFactory, IAspectBuilderFactory aspectBuilderFactory)
        {
            this.aspectContextFactory = aspectContextFactory;
            this.aspectBuilderFactory = aspectBuilderFactory;
        }

        public TResult Invoke<TResult>(AspectActivatorContext activatorContext)
        {
            var context = aspectContextFactory.CreateContext(activatorContext);
            try
            {
                var aspectBuilder = aspectBuilderFactory.Create(context);
                var task = aspectBuilder.Build()(context);
                if (task.IsFaulted)
                {
                    throw task.Exception.InnerException;
                }

                if (!task.IsCompleted)
                {
                    // try to avoid potential deadlocks.
                    NoSyncContextScope.Run(task);
                }
                return (TResult)context.ReturnValue;
            }
            finally
            {
                aspectContextFactory.ReleaseContext(context);
            }
        }

        public async Task<TResult> InvokeTask<TResult>(AspectActivatorContext activatorContext)
        {
            var context = aspectContextFactory.CreateContext(activatorContext);
            try
            {
                var aspectBuilder = aspectBuilderFactory.Create(context);
                await aspectBuilder.Build()(context);
                var result = context.ReturnValue;
                if (result is Task<TResult> taskWithResult)
                {
                    return await taskWithResult;
                }
                else if (result is Task task)
                {
                    await task;
                    return default(TResult);
                }
                else
                {
                    throw context.InvocationException(new InvalidCastException(
                        $"Unable to cast object of type '{result.GetType()}' to type '{typeof(Task<TResult>)}'."));
                }
            }
            finally
            {
                aspectContextFactory.ReleaseContext(context);
            }
        }

        public async ValueTask<TResult> InvokeValueTask<TResult>(AspectActivatorContext activatorContext)
        {
            var context = aspectContextFactory.CreateContext(activatorContext);
            try
            {
                var aspectBuilder = aspectBuilderFactory.Create(context);
                await aspectBuilder.Build()(context);
                return await (ValueTask<TResult>)context.ReturnValue;
            }
            finally
            {
                aspectContextFactory.ReleaseContext(context);
            }
        }
    }
}