﻿using FeatureFlowFramework.Helpers.Data;
using FeatureFlowFramework.Helpers.Diagnostics;
using System;
using System.Collections.Generic;
using System.Text;
using Xunit;

namespace FeatureFlowFramework.Helpers
{
    public class DistributedDataTests
    {
        [Fact]
        public void ChangedDataWillBePublishedToConnectedDistributedData()
        {
            TestHelper.PrepareTestContext();
            DistributedData<int> distributedData1 = new DistributedData<int>("myData");
            DistributedData<int> distributedData2 = new DistributedData<int>("myData");
            distributedData1.UpdateSender.ConnectTo(distributedData2.UpdateReceiver);
            distributedData2.UpdateSender.ConnectTo(distributedData1.UpdateReceiver);

            using (var access = distributedData1.Data.GetReadAccess())
            {
                Assert.Equal(default, access.Value);
            }
            using (var access = distributedData2.Data.GetReadAccess())
            {
                Assert.Equal(default, access.Value);
            }

            using (var access = distributedData1.Data.GetWriteAccess())
            {
                access.SetValue(42);
            }
            using (var access = distributedData2.Data.GetReadAccess())
            {
                Assert.Equal(42, access.Value);
            }

        }
    }
}
