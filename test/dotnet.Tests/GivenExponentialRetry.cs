﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli.Utils;

namespace Microsoft.DotNet.Tests
{
    public class GivenExponentialRetry : SdkTest
    {
        public GivenExponentialRetry(ITestOutputHelper log) : base(log)
        {
        }

        [Fact]
        public async Task ItReturnsOnSuccess()
        {
            var retryCount = 0;
            Func<Task<string>> action = () =>
            {
                retryCount++;
                return Task.FromResult("done");
            };
            var res = await ExponentialRetry.ExecuteWithRetryOnFailure<string>(action);

            retryCount.Should().Be(1);
        }

        [Fact]
        public async Task ItRetriesOnError()
        {
            var retryCount = 0;
            Func<Task<string?>> action = () => // Updated to use nullable reference type  
            {
                retryCount++;
                return Task.FromResult<string?>(null); // Updated to match nullable reference type  
            };
            var res = await ExponentialRetry.ExecuteWithRetryOnFailure<string?>(action, 2, timer: () => ExponentialRetry.Timer(ExponentialRetry.TestingIntervals));

            res.Should().BeNull();
            retryCount.Should().Be(2);
        }
    }
}
