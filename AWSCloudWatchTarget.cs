using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using NLog;
using NLog.Config;
using NLog.Targets;

using Amazon.CloudWatch;
using Amazon;
using Amazon.CloudWatch.Model;
using System.ComponentModel;
using NLog.Common;
using Amazon.Runtime;

namespace NLog.Targets.AWSCloudWatch
{
    [Target("AWSCloudWatch")]
    public sealed class AWSCloudWatchTarget : TargetWithLayout
    {

        private IAmazonCloudWatch client;

        [RequiredParameter]
        public string AwsAccessKey { get; set; }
        [RequiredParameter]
        public string AwsSecretAccessKey { get; set; }
        [DefaultValue("us-east-1")]
        public string Endpoint { get; set; }
        [RequiredParameter]
        public string Namespace { get; set; }
        [RequiredParameter]
        public string MetricName { get; set; }
        [DefaultValue("None")]
        public StandardUnit Unit { get; set; }
        [RequiredParameter]
        public double Value { get; set; }


        public AWSCloudWatchTarget()
        {
            Endpoint = "us-east-1";
            Unit = StandardUnit.None;
        }

        protected override void InitializeTarget()
        {
            base.InitializeTarget();

            InternalLogger.Debug("Initialising AWSCloudWatch '{0}' target", Endpoint);

            try
            {
                if (string.IsNullOrEmpty(AwsAccessKey) || string.IsNullOrEmpty(AwsSecretAccessKey))
                {
                    InternalLogger.Info("AWS Access Keys are not specified. Use Application Setting or EC2 Instance profile for keys.");
                    client = new AmazonCloudWatchClient(RegionEndpoint.GetBySystemName(Endpoint));
                }
                else
                {
                    var creditials = new BasicAWSCredentials(AwsAccessKey, AwsSecretAccessKey);
                    client = new AmazonCloudWatchClient(creditials, RegionEndpoint.GetBySystemName(Endpoint));
                }
            }
            catch (Exception e)
            {
                InternalLogger.Fatal("Amazon CloudWatch client failed to be configured and won't send any messages. Error is\n{0}\n{1}", e.Message, e.StackTrace);
            }

            InternalLogger.Debug("Initialised AWSCloudWatch '{0}' target", Endpoint);
        }


        protected override void Write(LogEventInfo logEvent)
        {
            InternalLogger.Debug("Sending log to AWSCloudWatch '{0}' {1}.{2}", Endpoint, this.Namespace, this.MetricName);
            try
            {
                string logMessage = this.Layout.Render(logEvent);

                var metricRequest = new PutMetricDataRequest { Namespace = this.Namespace };
                metricRequest.MetricData.Add(new MetricDatum
                {
                    MetricName = this.MetricName,
                    TimestampUtc = DateTime.UtcNow,
                    Unit = this.Unit,
                    Value = this.Value
                });

                try
                {           
                    var response = client.PutMetricData(metricRequest);

                    InternalLogger.Debug("Sent log to AWSCloudWatch, response received. HttpStatusCode: {0}, RequestId: {1}",
                       response.HttpStatusCode, response.ResponseMetadata.RequestId);
                }
                catch (InternalServiceException e)
                {
                    InternalLogger.Fatal("RequestId: {0}, ErrorType: {1}, Status: {2}\nFailed to send log with\n{3}\n{4}",
                        e.RequestId, e.ErrorType, e.StatusCode,
                        e.Message, e.StackTrace);
                }
            }
            catch (Exception e)
            {
                InternalLogger.Fatal("Failed to write log to Amazon CloudWatch with\n{0}\n{1}",
                       e.Message, e.StackTrace);
            }
        }

    }
}
