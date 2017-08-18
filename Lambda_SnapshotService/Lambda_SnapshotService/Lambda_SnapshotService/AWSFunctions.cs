using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Amazon.EC2;
using Amazon.EC2.Model;
using Amazon.KeyManagementService;
using Amazon.KeyManagementService.Model;

namespace Lambda_SnapshotService
{
    /*
     Defines all AWS Functions
    */
    public partial class Function
    {

        // PurgeEBSSnapshots
        // Purges snapshots for specified VolumeID
        // Keeps 2 weeks of daily's, then keeps one from all previous months
        // Ex: PurgeEBSSnapshots(string VolumeID, int RetentionDays)
        public static void PurgeEBSSnapshots(string VolumeID, int RetentionDays)
        {

            try
            {
                var CurrentDate = DateTime.Now;
                var RetentionDate = CurrentDate.AddDays(-RetentionDays);
                var CurrentDateString = DateTime.Now.ToString("MM/dd/yyyy h:mm tt");
                Console.WriteLine("Proceeding to check if purging of snapshots for specified Volume ID is needed, info below:\n");
                Console.WriteLine("\t:VolumeID: " + VolumeID);
                Console.WriteLine("\n\t:Current Time: " + CurrentDateString);
                Console.WriteLine("\n\t:Retention Period (Days): " + RetentionDays);


                // EC2 Client Constructor for C# Interactive Local Testing
                //      var reg = Amazon.RegionEndpoint.GetBySystemName(aws_region);
                //      AmazonEC2Client ec2client = new AmazonEC2Client(X, Y, reg);

                // Creates EC2 Client
                AmazonEC2Client ec2client = new AmazonEC2Client();

                // Gets current snapshots for the VolumeID
                var CurrentSnapshots = ec2client.DescribeSnapshotsAsync(new DescribeSnapshotsRequest
                {
                    Filters = new List<Amazon.EC2.Model.Filter> {
                        new Filter {
                            Name="volume-id",
                            Values=new List<string> { VolumeID }
                            },
                        new Filter {
                            Name="status",
                            Values=new List<string> { "completed" }
                            }
                         }
                }
                );

                // Creates list from snapshot results
                // foreach (var snap in SnapshotList) { Console.WriteLine(snap.StartTime); }
                var SnapshotList = CurrentSnapshots.Result.Snapshots;
                var SnapshotsToScan = SnapshotList.FindAll(item => item.StartTime <= RetentionDate);

                // Proceeds to go through all snaps older than retention date: deletes all except for one monthly
                if (SnapshotsToScan.Count > 0)
                {
                    for (int i = 1; i <= 12; i++)
                    {
                        var MonthlySnaps = SnapshotsToScan.FindAll(item => item.StartTime.Month == i);
                        var SnapsToDelete = MonthlySnaps.FindAll(item => item.StartTime.Day != 1);
                        foreach (Snapshot SnapToDelete in SnapsToDelete)
                        {
                            Console.WriteLine("Deleting the following snapshot:\n");
                            Console.WriteLine("\t:VolumeID: " + VolumeID);
                            Console.WriteLine("\n\t:Snapshot ID: " + SnapToDelete.SnapshotId);
                            Console.WriteLine("\n\t:Snapshot Start Time: " + SnapToDelete.StartTime);
                            var response = ec2client.DeleteSnapshotAsync(new DeleteSnapshotRequest
                            {
                                SnapshotId = SnapToDelete.SnapshotId
                            });

                        }
                    }

                }
                else
                {
                    Console.WriteLine("No snapshots found that need to be deleted:\n");
                }

            }
            catch (AggregateException ex)
            {

                foreach (var innerException in ex.Flatten().InnerExceptions)
                {
                    Console.WriteLine("\nFailed to purge snapshot for specified EBS Volume, inner exception info below:\n");
                    Console.WriteLine("Exception Type: " + innerException.GetType());
                    Console.WriteLine("Exception Message: " + innerException.Message);
                    Console.WriteLine("\nException Full Message below: \n" + ex.ToString());
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("\nFailed to purge snapshot for specified EBS Volume, exception info below:\n");
                Console.WriteLine("Exception Type: " + ex.GetType());
                Console.WriteLine("Exception Message: " + ex.Message);
                Console.WriteLine("\nException Full Message below: \n" + ex.ToString());
            }

        }

        // CreateEBSSnapshot
        // Snapshots specified volume id
        // CreateEBSSnapshot(VolumeID);
        public static void CreateEBSSnapshot(string VolumeID)
        {

            try
            {

                // string VolumeID="X";
                var CurrentDate = DateTime.Now.ToString("MM/dd/yyyy h:mm tt");
                Console.WriteLine("Proceeding to create snapshot for specified Volume ID, info below:\n");
                Console.WriteLine("\t:VolumeID: " + VolumeID);
                Console.WriteLine("\n\t:Current Time: " + CurrentDate);


                // EC2 Client Constructor for C# Interactive Local Testing
                //      var reg = Amazon.RegionEndpoint.GetBySystemName(aws_region);
                //      AmazonEC2Client ec2client = new AmazonEC2Client(X, Y, reg);

                // Creates EC2 Client
                AmazonEC2Client ec2client = new AmazonEC2Client();

                // Creates snapshot request
                var SnapshotResponse = ec2client.CreateSnapshotAsync(new CreateSnapshotRequest
                {
                    Description = "Lambda Automated Snapshot - Volume ID: " + VolumeID + " - Current Time: " + CurrentDate,
                    VolumeId = VolumeID
                });

                // Gets info from response
                string SnapshotID = SnapshotResponse.Result.Snapshot.SnapshotId;
                string SnapshotState = SnapshotResponse.Result.Snapshot.State;
                string SnapshotStartTime = SnapshotResponse.Result.Snapshot.StartTime.ToString();
                Console.WriteLine("Successfully created snapshot, info below:\n");
                Console.WriteLine("\t:VolumeID: " + VolumeID);
                Console.WriteLine("\n\t:Snapshot ID: " + SnapshotID);
                Console.WriteLine("\n\t:Snapshot Start Time: " + SnapshotStartTime);
                Console.WriteLine("\n\t:Snapshot State: " + SnapshotState);


            }
            catch (AggregateException ex)
            {

                foreach (var innerException in ex.Flatten().InnerExceptions)
                {
                    Console.WriteLine("\nFailed to create snapshot for specified EBS Volume, inner exception info below:\n");
                    Console.WriteLine("Exception Type: " + innerException.GetType());
                    Console.WriteLine("Exception Message: " + innerException.Message);
                    Console.WriteLine("\nException Full Message below: \n" + ex.ToString());
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("\nFailed to create snapshot for specified EBS Volume, exception info below:\n");
                Console.WriteLine("Exception Type: " + ex.GetType());
                Console.WriteLine("Exception Message: " + ex.Message);
                Console.WriteLine("\nException Full Message below: \n" + ex.ToString());
            }

        }


        // GetEBSVolumesForInstance
        // Returns EBS Volume ID's for a specified instance
        // Ex: var VolumeIDs=GetEBSVolumesForInstance(InstanceID);
        public static List<string> GetEBSVolumesForInstance(string InstanceID)
        {

            try
            {
                Console.WriteLine("Getting the EBS Voumes for InstanceID: " + InstanceID + "\n");
                // Creates list to return
                List<string> VolumeIDList = new List<string>();


                // EC2 Client Constructor for C# Interactive Local Testing
                //      var reg = Amazon.RegionEndpoint.GetBySystemName(aws_region);
                //      AmazonEC2Client ec2client = new AmazonEC2Client(X, Y, reg);

                // Creates EC2 Client
                AmazonEC2Client ec2client = new AmazonEC2Client();
                var InstanceRequest = new DescribeInstancesRequest();
                InstanceRequest.InstanceIds.Add(InstanceID);
                var InstanceResponse = ec2client.DescribeInstancesAsync(InstanceRequest);

                // Gets list of IDs from response
                List<Reservation> Results = InstanceResponse.Result.Reservations;

                if (Results.Count >= 1)
                {
                    foreach (Reservation Res in Results)
                    {
                        List<Instance> Instances = Res.Instances;
                        foreach (Instance Ins in Instances)
                        {
                            List<InstanceBlockDeviceMapping> EBSVolumes = Ins.BlockDeviceMappings;
                            foreach (InstanceBlockDeviceMapping device in EBSVolumes)
                            {
                                VolumeIDList.Add(device.Ebs.VolumeId);

                            }

                        }

                    }
                    Console.WriteLine("Found the following Volumes for the Instance: \n");
                    Console.WriteLine("\t" + String.Join(", ", VolumeIDList.ToArray()) + "\n");
                    return VolumeIDList;
                }
                else
                {
                    Console.WriteLine("Specified Instance has no EBS Volumes\n");
                    return null;

                }


            }
            catch (AggregateException ex)
            {

                foreach (var innerException in ex.Flatten().InnerExceptions)
                {
                    Console.WriteLine("\nFailed to retrieve EBS Volumes, inner exception info below:\n");
                    Console.WriteLine("Exception Type: " + innerException.GetType());
                    Console.WriteLine("Exception Message: " + innerException.Message);
                    Console.WriteLine("\nException Full Message below: \n" + ex.ToString());
                }
                return null;
            }
            catch (Exception ex)
            {
                Console.WriteLine("\nFailed to retrieve EBS Volumes, exception info below:\n");
                Console.WriteLine("Exception Type: " + ex.GetType());
                Console.WriteLine("Exception Message: " + ex.Message);
                Console.WriteLine("\nException Full Message below: \n" + ex.ToString());
                return null;

            }

        }


        // GetInstancesByTagValue
        // Returns Instances that contain a specified key\pair tag
        // Ex: var instance = GetInstancesByTagValue("SnapshotSchedule", "Daily");
        public static List<string> GetInstancesByTagValue(string key, string value)
        {

            try
            {

                // string key="tag:" +"Snapshot";
                // string value="Daily";
                Console.WriteLine("Getting Instances with the following tag:\n\tKey: " + key + "\n\tValue: " + value);

                // Creates lists
                string inputkey = "tag:" + key;
                List<string> InstanceList = new List<string>();
                List<string> ValueList = new List<string>();
                List<Amazon.EC2.Model.Filter> FilterList = new List<Amazon.EC2.Model.Filter>();
                ValueList.Add(value);

                // EC2 Client Constructor for C# Interactive Local Testing
                //      var reg = Amazon.RegionEndpoint.GetBySystemName(aws_region);
                //      AmazonEC2Client ec2client = new AmazonEC2Client(X, Y, reg);

                // Creates EC2 Client
                var filter = new Filter(inputkey, ValueList);
                FilterList.Add(filter);
                AmazonEC2Client ec2client = new AmazonEC2Client();
                var InstanceRequest = new DescribeInstancesRequest();
                InstanceRequest.Filters = FilterList;
                var InstanceResponse = ec2client.DescribeInstancesAsync(InstanceRequest);

                // Gets list of IDs from response
                List<Reservation> Results = InstanceResponse.Result.Reservations;

                if (Results.Count >= 1)
                {
                    foreach (Reservation Res in Results)
                    {
                        List<Instance> Instances = Res.Instances;
                        foreach (Instance Ins in Instances)
                        {
                            InstanceList.Add(Ins.InstanceId);

                        }

                    }
                    Console.WriteLine("Found the following instances with the specified key value pair.\n");
                    Console.WriteLine("\t" + String.Join(", ", InstanceList.ToArray()) + "\n");
                    return InstanceList;
                }
                else
                {
                    Console.WriteLine("No Instances found with the specified key value pair.\n");
                    return null;

                }


            }
            catch (AggregateException ex)
            {

                foreach (var innerException in ex.Flatten().InnerExceptions)
                {
                    Console.WriteLine("\nFailed to retrieve instances, inner exception info below:\n");
                    Console.WriteLine("Exception Type: " + innerException.GetType());
                    Console.WriteLine("Exception Message: " + innerException.Message);
                    Console.WriteLine("\nException Full Message below: \n" + ex.ToString());
                }
                return null;
            }
            catch (Exception ex)
            {
                Console.WriteLine("\nFailed to retrieve instances, exception info below:\n");
                Console.WriteLine("Exception Type: " + ex.GetType());
                Console.WriteLine("Exception Message: " + ex.Message);
                Console.WriteLine("\nException Full Message below: \n" + ex.ToString());
                return null;

            }

        }


        // DecryptString
        // Returns plaintext string from encrypted string using AWS KMS
        public static string DecryptString(string encryptedstring)
        {
            try
            {
                Console.WriteLine("Decrypting the following encrypted string :" + encryptedstring);
                MemoryStream inputstream = new MemoryStream(Convert.FromBase64String(encryptedstring));
                var decryptRequest = new DecryptRequest();
                decryptRequest.CiphertextBlob = inputstream;
                var client = new AmazonKeyManagementServiceClient();
                var decryptRespose = client.DecryptAsync(decryptRequest);
                var results = new StreamReader(decryptRespose.Result.Plaintext).ReadToEnd();
                Console.WriteLine("\nSuccessfully decrypted the string\n");
                return results;
            }

            catch (AggregateException ex)
            {

                foreach (var innerException in ex.Flatten().InnerExceptions)
                {
                    Console.WriteLine("\nFailed to decrypt the string, inner exception info below:\n");
                    Console.WriteLine("Exception Type: " + innerException.GetType());
                    Console.WriteLine("Exception Message: " + innerException.Message);
                    Console.WriteLine("\nException Full Message below: \n" + ex.ToString());
                }
                return null;
            }
            catch (Exception ex)
            {
                Console.WriteLine("\nFailed to decrypt the string, exception info below:\n");
                Console.WriteLine("Exception Type: " + ex.GetType());
                Console.WriteLine("Exception Message: " + ex.Message);
                Console.WriteLine("\nException Full Message below: \n" + ex.ToString());
                return null;

            }

        }


        // GetInstanceName
        // Returns Instance Name as string from ID
        // Ex: string InstanceName=GetInstanceName(instanceid);
        public static string GetInstanceName(string instanceid)
        {

            Console.WriteLine("Getting Instance Name for InstanceID :" + instanceid);
            // Creates describe instances request and adds the specified instance id
            DescribeInstancesRequest instanceRequest = new DescribeInstancesRequest();
            instanceRequest.InstanceIds.Add(instanceid);

            // Creates ec2 client and attempts to get its name tag
            try
            {
                // EC2 Client Constructor for C# Interactive Local Testing
                //      var reg = Amazon.RegionEndpoint.GetBySystemName(aws_region);
                //      AmazonEC2Client ec2client = new AmazonEC2Client(X, Y, reg);
                AmazonEC2Client ec2client = new AmazonEC2Client();
                var instanceResponse = ec2client.DescribeInstancesAsync(instanceRequest);
                var tags = instanceResponse.Result.Reservations[0].Instances[0].Tags;
                var tag = tags.Find(item => item.Key == "Name");
                string name = tag.Value;
                Console.WriteLine("Instance Name is: " + name);
                return name;
            }

            catch (AggregateException ex)
            {

                foreach (var innerException in ex.Flatten().InnerExceptions)
                {
                    Console.WriteLine("\nFailed to get Instance name, inner exception info below:\n");
                    Console.WriteLine("Exception Type: " + innerException.GetType());
                    Console.WriteLine("Exception Message: " + innerException.Message);
                    Console.WriteLine("\nException Full Message below: \n" + ex.ToString());
                }
                return null;
            }
            catch (Exception ex)
            {
                Console.WriteLine("\nFailed to get Instance name, exception info below:\n");
                Console.WriteLine("Exception Type: " + ex.GetType());
                Console.WriteLine("Exception Message: " + ex.Message);
                Console.WriteLine("\nException Full Message below: \n" + ex.ToString());
                return null;

            }

        }

        // TestFailure
        // Forces Exception to be hit - for troubleshooting purposes
        // Ex: TestFailure()
        public static void TestFailure()
        {

            try
            {

                Console.WriteLine("Invoking TestFailure Function\n");
                throw new Exception();
                //System.Environment.Exit(-1);

            }
            catch (Exception ex)
            {
                Console.WriteLine("\nTestFailure Exception Hit, exception info below:\n");
                Console.WriteLine("Exception Type: " + ex.GetType());
                Console.WriteLine("Exception Message: " + ex.Message);
                Console.WriteLine("\nException Full Message below: \n" + ex.ToString());
                throw;
                //System.Environment.Exit(-1);
            }

        }

    }
}