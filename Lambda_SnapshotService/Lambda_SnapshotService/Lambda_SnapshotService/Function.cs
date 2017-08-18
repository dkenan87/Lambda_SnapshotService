using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.IO;
using Amazon.Lambda.Core;

// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.Json.JsonSerializer))]

namespace Lambda_SnapshotService
{
    public partial class Function
    {

        /// <summary>
        /// Lambda Function that takes daily EC2 snapshots and creates retention schedule
        /// No parameters needed - feeds off of instance tags (SnapshotSchedule : Daily)
        /// </summary>
        ///
        public void FunctionHandler(Stream InputStream, ILambdaContext context)
        {
            try
            {
                // Gets Instances with the specified tag values
                var Instances = GetInstancesByTagValue("x", "z");
                // Sets Retention in days
                int RetentionDays = 14;

                //Tests Failure TestFailure();

                // Proceeds if any instances found
                if (Instances.Count > 0)
                {
                    foreach (var Instance in Instances)
                    {
                        // Retrieves Instance Name
                        string InstanceName = GetInstanceName(Instance);
                        Console.WriteLine("Proceeding with Instance: " + InstanceName);

                        // Retrieves all EBS volumes attached to instance
                        var VolumeIDs = GetEBSVolumesForInstance(Instance);

                        // Proceeds if VolumeIDs found
                        if (VolumeIDs.Count > 0)
                        {
                            foreach (var VolumeID in VolumeIDs)
                            {
                                // Creates snapshot of specified volume
                                CreateEBSSnapshot(VolumeID);

                                // Runs Purge function to cleanup old snapshot based on retention period
                                PurgeEBSSnapshots(VolumeID, RetentionDays);
                            }

                        }

                    }

                }
            }
            catch (AggregateException ex)
            {

                foreach (var innerException in ex.Flatten().InnerExceptions)
                {
                    Console.WriteLine("\nLambda Function Failed, inner exception info below:\n");
                    Console.WriteLine("Exception Type: " + innerException.GetType());
                    Console.WriteLine("Exception Message: " + innerException.Message);
                    Console.WriteLine("\nException Full Message below: \n" + ex.ToString());

                }
                System.Environment.Exit(-1);
            }
            catch (Exception ex)
            {
                Console.WriteLine("\nLambda Function Failed, exception info below:\n");
                Console.WriteLine("Exception Type: " + ex.GetType());
                Console.WriteLine("Exception Message: " + ex.Message);
                Console.WriteLine("\nException Full Message below: \n" + ex.ToString());
                System.Environment.Exit(-1);
            }

        }
    }
}
