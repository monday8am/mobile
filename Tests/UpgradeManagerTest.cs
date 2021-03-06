﻿using System;
using System.Linq;
using NUnit.Framework;
using Toggl.Phoebe.Analytics;
using Toggl.Phoebe.Data;
using Toggl.Phoebe.Data.DataObjects;
using XPlatUtils;

namespace Toggl.Phoebe.Tests
{
    [TestFixture]
    public class UpgradeManagerTest : Test
    {
        private PlatformInfo platformInfo;
        private SettingStore settingStore;
        private UpgradeManger upgradeManager;

        public override void SetUp ()
        {
            base.SetUp ();

            ServiceContainer.Register<IPlatformInfo> (platformInfo = new PlatformInfo ());
            ServiceContainer.Register<ISettingsStore> (settingStore = new SettingStore ());
            ServiceContainer.Register<ExperimentManager> (new ExperimentManager ());
            upgradeManager = new UpgradeManger ();
        }

        [Test]
        public void TestAnyUpgrade ()
        {
            RunAsync (async delegate {
                platformInfo.AppVersion = "1.0.1";
                settingStore.SyncLastRun = new DateTime (2014, 1, 2, 10, 0, 0, DateTimeKind.Utc);

                var workspaceData = await DataStore.PutAsync (new WorkspaceData () {
                    RemoteId = 2,
                    IsDirty = false,
                    RemoteRejected = false,
                    DeletedAt = null,
                    Name = "Test",
                    ModifiedAt = new DateTime (2014, 1, 2),
                });
                var userData = await DataStore.PutAsync (new UserData () {
                    RemoteId = 2,
                    IsDirty = true,
                    RemoteRejected = true,
                    DeletedAt = null,
                    Name = "John",
                    DefaultWorkspaceId = workspaceData.Id,
                    ModifiedAt = new DateTime (2014, 1, 2),
                });
                var timeEntry1 = await DataStore.PutAsync (new TimeEntryData () {
                    RemoteId = 2,
                    IsDirty = false,
                    RemoteRejected = false,
                    DeletedAt = new DateTime (2014, 1, 2),
                    Description = "Testing...",
                    WorkspaceId = workspaceData.Id,
                    UserId = userData.Id,
                    ModifiedAt = new DateTime (2014, 1, 2),
                });
                var timeEntry2 = await DataStore.PutAsync (new TimeEntryData () {
                    RemoteId = 3,
                    IsDirty = true,
                    RemoteRejected = false,
                    DeletedAt = null,
                    Description = "Testing...",
                    WorkspaceId = workspaceData.Id,
                    UserId = userData.Id,
                    ModifiedAt = new DateTime (2014, 1, 2),
                });

                upgradeManager.TryUpgrade ();

                Assert.AreEqual (platformInfo.AppVersion, settingStore.LastAppVersion);
                Assert.IsNull (settingStore.SyncLastRun);

                workspaceData = (await DataStore.Table<WorkspaceData> ().QueryAsync (r => r.Id == workspaceData.Id)).Single ();
                userData = (await DataStore.Table<UserData> ().QueryAsync (r => r.Id == userData.Id)).Single ();
                timeEntry1 = (await DataStore.Table<TimeEntryData> ().QueryAsync (r => r.Id == timeEntry1.Id)).Single ();
                timeEntry2 = (await DataStore.Table<TimeEntryData> ().QueryAsync (r => r.Id == timeEntry2.Id)).Single ();

                Assert.AreEqual (DateTime.MinValue, workspaceData.ModifiedAt);
                Assert.AreEqual (DateTime.MinValue, userData.ModifiedAt);
                Assert.AreNotEqual (DateTime.MinValue, timeEntry1.ModifiedAt);
                Assert.AreNotEqual (DateTime.MinValue, timeEntry2.ModifiedAt);
            });
        }

        private class PlatformInfo : IPlatformInfo
        {
            public string AppIdentifier { get; set; }

            public string AppVersion { get; set; }
        }

        private class SettingStore : ISettingsStore
        {
            public Guid? UserId { get; set; }

            public string ApiToken { get; set; }

            public DateTime? SyncLastRun { get; set; }

            public bool UseDefaultTag { get; set; }

            public string LastAppVersion { get; set; }

            public string ExperimentId { get; set; }

            public int? LastReportZoomViewed { get; set; }
        }
    }
}
