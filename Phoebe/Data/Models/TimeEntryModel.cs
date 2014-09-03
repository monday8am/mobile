using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Toggl.Phoebe.Data.DataObjects;
using Toggl.Phoebe.Net;
using XPlatUtils;

namespace Toggl.Phoebe.Data.Models
{
    public class TimeEntryModel : Model<TimeEntryData>
    {
        private const string Tag = "TimeEntryModel";

        private static string GetPropertyName<T> (Expression<Func<TimeEntryModel, T>> expr)
        {
            return expr.ToPropertyName ();
        }

        private static bool ShouldAddDefaultTag {
            get { return ServiceContainer.Resolve<ISettingsStore> ().UseDefaultTag; }
        }

        internal static readonly string DefaultTag = "mobile";
        public static new readonly string PropertyId = Model<TimeEntryData>.PropertyId;
        public static readonly string PropertyState = GetPropertyName (m => m.State);
        public static readonly string PropertyDescription = GetPropertyName (m => m.Description);
        public static readonly string PropertyStartTime = GetPropertyName (m => m.StartTime);
        public static readonly string PropertyStopTime = GetPropertyName (m => m.StopTime);
        public static readonly string PropertyDurationOnly = GetPropertyName (m => m.DurationOnly);
        public static readonly string PropertyIsBillable = GetPropertyName (m => m.IsBillable);
        public static readonly string PropertyUser = GetPropertyName (m => m.User);
        public static readonly string PropertyWorkspace = GetPropertyName (m => m.Workspace);
        public static readonly string PropertyProject = GetPropertyName (m => m.Project);
        public static readonly string PropertyTask = GetPropertyName (m => m.Task);

        public TimeEntryModel ()
        {
        }

        public TimeEntryModel (TimeEntryData data) : base (data)
        {
        }

        public TimeEntryModel (Guid id) : base (id)
        {
        }

        protected override TimeEntryData Duplicate (TimeEntryData data)
        {
            return new TimeEntryData (data);
        }

        protected override void OnBeforeSave ()
        {
            if (Data.UserId == Guid.Empty) {
                throw new ValidationException ("User must be set for TimeEntry model.");
            }
            if (Data.WorkspaceId == Guid.Empty) {
                throw new ValidationException ("Workspace must be set for TimeEntry model.");
            }
        }

        protected override void DetectChangedProperties (TimeEntryData oldData, TimeEntryData newData)
        {
            base.DetectChangedProperties (oldData, newData);
            if (oldData.State != newData.State)
                OnPropertyChanged (PropertyState);
            if (ReturnEmptyIfNull (oldData.Description) != ReturnEmptyIfNull (newData.Description))
                OnPropertyChanged (PropertyDescription);
            if (oldData.StartTime != newData.StartTime)
                OnPropertyChanged (PropertyStartTime);
            if (oldData.StopTime != newData.StopTime)
                OnPropertyChanged (PropertyStopTime);
            if (oldData.DurationOnly != newData.DurationOnly)
                OnPropertyChanged (PropertyDurationOnly);
            if (oldData.IsBillable != newData.IsBillable)
                OnPropertyChanged (PropertyIsBillable);
            if (oldData.UserId != newData.UserId || user.IsNewInstance)
                OnPropertyChanged (PropertyUser);
            if (oldData.WorkspaceId != newData.WorkspaceId || workspace.IsNewInstance)
                OnPropertyChanged (PropertyWorkspace);
            if (oldData.ProjectId != newData.ProjectId || project.IsNewInstance)
                OnPropertyChanged (PropertyProject);
            if (oldData.TaskId != newData.TaskId || task.IsNewInstance)
                OnPropertyChanged (PropertyTask);
        }

        private string ReturnEmptyIfNull (String s)
        {
            return String.IsNullOrEmpty (s) ? String.Empty : s;
        }

        public TimeEntryState State {
            get {
                EnsureLoaded ();
                return Data.State;
            }
            set {
                if (State == value)
                    return;

                MutateData (data => {
                    // Adjust start-time to keep duration same when switching to running state
                    if (value == TimeEntryState.Running && data.StopTime.HasValue) {
                        var duration = GetDuration (data);
                        var now = Time.UtcNow;
                        data.StopTime = null;
                        data.StartTime = (now - duration).Truncate (TimeSpan.TicksPerSecond);
                    }

                    data.State = value;
                });
            }
        }

        public string Description {
            get {
                EnsureLoaded ();
                return ReturnEmptyIfNull (Data.Description);
            }
            set {
                value = ReturnEmptyIfNull (value);

                if (Description == value)
                    return;

                MutateData (data => data.Description = value);
            }
        }

        public DateTime StartTime {
            get {
                EnsureLoaded ();
                return Data.StartTime;
            }
            set {
                value = value.ToUtc ().Truncate (TimeSpan.TicksPerSecond);
                if (StartTime == value)
                    return;

                MutateData (data => {
                    var duration = GetDuration (data);

                    data.StartTime = value;

                    if (State != TimeEntryState.Running) {
                        if (data.StopTime.HasValue) {
                            data.StopTime = data.StartTime + duration;
                        } else {
                            var now = Time.UtcNow;

                            data.StopTime = data.StartTime.Date
                                    .AddHours (now.Hour)
                                    .AddMinutes (now.Minute)
                                    .AddSeconds (data.StartTime.Second);

                            if (data.StopTime < data.StartTime) {
                                data.StopTime = data.StartTime + duration;
                            }
                        }

                        data.StartTime = data.StartTime.Truncate (TimeSpan.TicksPerSecond);
                        data.StopTime = data.StopTime.Truncate (TimeSpan.TicksPerSecond);
                    }
                });
            }
        }

        public DateTime? StopTime {
            get {
                EnsureLoaded ();
                return Data.StopTime;
            }
            set {
                value = value.ToUtc ().Truncate (TimeSpan.TicksPerSecond);
                if (StopTime == value)
                    return;

                MutateData (data => data.StopTime = value);
            }
        }

        public bool DurationOnly {
            get {
                EnsureLoaded ();
                return Data.DurationOnly;
            }
            set {
                if (DurationOnly == value)
                    return;

                MutateData (data => data.DurationOnly = value);
            }
        }

        public bool IsBillable {
            get {
                EnsureLoaded ();
                return Data.IsBillable;
            }
            set {
                if (IsBillable == value)
                    return;

                MutateData (data => data.IsBillable = value);
            }
        }

        public string FormattedDuration {
            get {
                TimeSpan duration = GetDuration (Data, Time.UtcNow);
                System.Diagnostics.Debug.WriteLine (this.User.DurationFormat);
                string formattedString = GetDuration (Data, Time.UtcNow).ToString (@"h\:mm\:ss");

                if (this.User.DurationFormat == DurationFormat.Classic) {
                    if (duration.TotalMinutes < 1)
                        formattedString = duration.ToString (@"s\ \s\e\c");
                    else if (duration.TotalMinutes > 1 && duration.TotalMinutes < 60)
                        formattedString = duration.ToString (@"mm\:ss\ \m\i");
                    else
                        formattedString = duration.ToString (@"hh\:mm\:ss"); 
                } else if (this.User.DurationFormat == DurationFormat.Decimal) {
                    formattedString = String.Format ("{0:0.00} h", duration.TotalHours); 
                }
                return formattedString;
            }
            private set { }
        }

        public TimeSpan GetDuration ()
        {
            return GetDuration (Data, Time.UtcNow);
        }

        private TimeSpan GetDuration (TimeEntryData data)
        {
            return GetDuration (data, Time.UtcNow);
        }

        private static TimeSpan GetDuration (TimeEntryData data, DateTime now)
        {
            if (data.StartTime == DateTime.MinValue) {
                return TimeSpan.Zero;
            }

            var duration = (data.StopTime ?? now) - data.StartTime;
            if (duration < TimeSpan.Zero) {
                duration = TimeSpan.Zero;
            }
            return duration;
        }

        public void SetDuration (TimeSpan value)
        {
            MutateData (data => SetDuration (data, value));
        }

        private static void SetDuration (TimeEntryData data, TimeSpan value)
        {
            var now = Time.UtcNow;

            if (data.State == TimeEntryState.Finished) {
                data.StopTime = data.StartTime + value;
            } else if (data.State == TimeEntryState.New) {
                if (value == TimeSpan.Zero) {
                    data.StartTime = DateTime.MinValue;
                    data.StopTime = null;
                } else if (data.StopTime.HasValue) {
                    data.StartTime = data.StopTime.Value - value;
                } else {
                    data.StartTime = now - value;
                    data.StopTime = now;
                }
            } else {
                data.StartTime = now - value;
            }

            data.StartTime = data.StartTime.Truncate (TimeSpan.TicksPerSecond);
            data.StopTime = data.StopTime.Truncate (TimeSpan.TicksPerSecond);
        }

        private ForeignRelation<UserModel> user;
        private ForeignRelation<WorkspaceModel> workspace;
        private ForeignRelation<ProjectModel> project;
        private ForeignRelation<TaskModel> task;

        protected override void InitializeRelations ()
        {
            base.InitializeRelations ();

            user = new ForeignRelation<UserModel> () {
                ShouldLoad = EnsureLoaded,
                Factory = id => new UserModel (id),
                Changed = m => MutateData (data => data.UserId = m.Id),
            };

            workspace = new ForeignRelation<WorkspaceModel> () {
                ShouldLoad = EnsureLoaded,
                Factory = id => new WorkspaceModel (id),
                Changed = m => MutateData (data => data.WorkspaceId = m.Id),
            };

            project = new ForeignRelation<ProjectModel> () {
                Required = false,
                ShouldLoad = EnsureLoaded,
                Factory = id => new ProjectModel (id),
                Changed = m => MutateData (data => {
                    if (m != null) {
                        data.IsBillable = m.IsBillable;
                    }
                    data.ProjectId = GetOptionalId (m);
                }),
            };

            task = new ForeignRelation<TaskModel> () {
                Required = false,
                ShouldLoad = EnsureLoaded,
                Factory = id => new TaskModel (id),
                Changed = m => MutateData (data => data.TaskId = GetOptionalId (m)),
            };
        }

        [ModelRelation]
        public UserModel User {
            get { return user.Get (Data.UserId); }
            set { user.Set (value); }
        }

        [ModelRelation]
        public WorkspaceModel Workspace {
            get { return workspace.Get (Data.WorkspaceId); }
            set { workspace.Set (value); }
        }

        [ModelRelation (Required = false)]
        public ProjectModel Project {
            get { return project.Get (Data.ProjectId); }
            set { project.Set (value); }
        }

        [ModelRelation (Required = false)]
        public TaskModel Task {
            get { return task.Get (Data.TaskId); }
            set { task.Set (value); }
        }

        /// <summary>
        /// Stores the draft time entry in model store as a running time entry.
        /// </summary>
        public async Task StartAsync ()
        {
            await LoadAsync ();

            if (Data.State != TimeEntryState.New)
                throw new InvalidOperationException (String.Format ("Cannot start a time entry in {0} state.", Data.State));
            if (Data.StartTime != DateTime.MinValue || Data.StopTime.HasValue)
                throw new InvalidOperationException ("Cannot start tracking time entry with start/stop time set already.");

            var task = Task;
            var project = Project;
            var workspace = Workspace;
            var user = User;

            // Preload all pending relations:
            var pending = new List<Task> ();
            if (task != null)
                pending.Add (task.LoadAsync ());
            if (project != null)
                pending.Add (project.LoadAsync ());
            if (workspace != null)
                pending.Add (workspace.LoadAsync ());
            if (user != null)
                pending.Add (user.LoadAsync ());
            await System.Threading.Tasks.Task.WhenAll (pending);

            if (ModelExists (task))
                project = task.Project;
            if (ModelExists (project))
                workspace = project.Workspace;
            if (ModelExists (user) && !ModelExists (workspace))
                workspace = user.DefaultWorkspace;
            if (!ModelExists (workspace))
                throw new InvalidOperationException ("Workspace (or user default workspace) must be set.");

            MutateData (data => {
                data.TaskId = task != null ? (Guid?)task.Id : null;
                data.ProjectId = project != null ? (Guid?)project.Id : null;
                data.WorkspaceId = workspace.Id;
                data.UserId = user.Id;
                data.State = TimeEntryState.Running;
                data.StartTime = Time.UtcNow;
                data.StopTime = null;
            });

            await SaveAsync ();
            await AddDefaultTags ();
        }

        private async Task AddDefaultTags ()
        {
            if (!ShouldAddDefaultTag)
                return;
            var dataStore = ServiceContainer.Resolve<IDataStore> ();
            var timeEntryId = Data.Id;
            var workspaceId = Data.WorkspaceId;

            await dataStore.ExecuteInTransactionAsync (ctx => AddDefaultTags (ctx, workspaceId, timeEntryId))
                .ConfigureAwait (false);
        }

        private static void AddDefaultTags (IDataStoreContext ctx, Guid workspaceId, Guid timeEntryId)
        {
            // Check that there aren't any tags set yet:
            var tagCount = ctx.Connection.Table<TimeEntryTagData> ()
                .Count (r => r.TimeEntryId == timeEntryId && r.DeletedAt == null);
            if (tagCount > 0)
                return;

            var defaultTag = ctx.Connection.Table<TagData> ()
                .Where (r => r.Name == DefaultTag && r.WorkspaceId == workspaceId && r.DeletedAt == null)
                .FirstOrDefault ();

            if (defaultTag == null) {
                defaultTag = ctx.Put (new TagData () {
                    Name = DefaultTag,
                    WorkspaceId = workspaceId,
                });
            }

            ctx.Put (new TimeEntryTagData () {
                TimeEntryId = timeEntryId,
                TagId = defaultTag.Id,
            });
        }

        /// <summary>
        /// Stores the draft time entry in model store as a finished time entry.
        /// </summary>
        public async Task StoreAsync ()
        {
            await LoadAsync ();

            if (Data.State != TimeEntryState.New)
                throw new InvalidOperationException (String.Format ("Cannot store a time entry in {0} state.", Data.State));
            if (Data.StartTime == DateTime.MinValue || Data.StopTime == null)
                throw new InvalidOperationException ("Cannot store time entry with start/stop time not set.");

            var task = Task;
            var project = Project;
            var workspace = Workspace;
            var user = User;

            // Preload all pending relations:
            var pending = new List<Task> ();
            if (task != null)
                pending.Add (task.LoadAsync ());
            if (project != null)
                pending.Add (project.LoadAsync ());
            if (workspace != null)
                pending.Add (workspace.LoadAsync ());
            if (user != null)
                pending.Add (user.LoadAsync ());
            await System.Threading.Tasks.Task.WhenAll (pending);

            if (ModelExists (task))
                project = task.Project;
            if (ModelExists (project))
                workspace = project.Workspace;
            if (ModelExists (user) && !ModelExists (workspace))
                workspace = user.DefaultWorkspace;
            if (!ModelExists (workspace))
                throw new InvalidOperationException ("Workspace (or user default workspace) must be set.");

            MutateData (data => {
                data.TaskId = task != null ? (Guid?)task.Id : null;
                data.ProjectId = project != null ? (Guid?)project.Id : null;
                data.WorkspaceId = workspace.Id;
                data.UserId = user.Id;
                data.State = TimeEntryState.Finished;
            });

            await SaveAsync ();
            await AddDefaultTags ();
        }

        /// <summary>
        /// Marks the currently running time entry as finished.
        /// </summary>
        public async Task StopAsync ()
        {
            await LoadAsync ();

            if (Data.State != TimeEntryState.Running)
                throw new InvalidOperationException (String.Format ("Cannot stop a time entry in {0} state.", Data.State));

            MutateData (data => {
                data.State = TimeEntryState.Finished;
                data.StopTime = Time.UtcNow;
            });

            await SaveAsync ();
        }

        /// <summary>
        /// Continues the finished time entry, either by creating a new time entry or restarting the current one.
        /// </summary>
        public async Task<TimeEntryModel> ContinueAsync ()
        {
            var store = ServiceContainer.Resolve<IDataStore> ();

            await LoadAsync ();

            // Validate the current state
            switch (Data.State) {
            case TimeEntryState.Running:
                return this;
            case TimeEntryState.Finished:
                break;
            default:
                throw new InvalidOperationException (String.Format ("Cannot continue a time entry in {0} state.", Data.State));
            }

            // We can continue time entries which haven't been synced yet:
            if (Data.DurationOnly && Data.StartTime.ToLocalTime ().Date == Time.Now.Date) {
                if (Data.RemoteId == null) {
                    MutateData (data => {
                        data.State = TimeEntryState.Running;
                        data.StartTime = Time.UtcNow - GetDuration ();
                        data.StopTime = null;
                    });

                    await SaveAsync ();
                    return this;
                }
            }

            // Create new time entry:
            var newData = new TimeEntryData () {
                WorkspaceId = Data.WorkspaceId,
                ProjectId = Data.ProjectId,
                TaskId = Data.TaskId,
                UserId = Data.UserId,
                Description = Data.Description,
                StartTime = Time.UtcNow,
                DurationOnly = Data.DurationOnly,
                IsBillable = Data.IsBillable,
                State = TimeEntryState.Running,
            };
            MarkDirty (newData);

            var parentId = Data.Id;
            await store.ExecuteInTransactionAsync (ctx => {
                newData = ctx.Put (newData);

                // Duplicate tag relations as well
                if (parentId != Guid.Empty) {
                    var q = ctx.Connection.Table<TimeEntryTagData> ()
                        .Where (r => r.TimeEntryId == parentId && r.DeletedAt == null);
                    foreach (var row in q) {
                        ctx.Put (new TimeEntryTagData () {
                            TimeEntryId = newData.Id,
                            TagId = row.TagId,
                        });
                    }
                }
            });

            var model = new TimeEntryModel (newData);

            return model;
        }

        private static TaskCompletionSource<TimeEntryData> draftDataTCS;

        public static async Task<TimeEntryModel> GetDraftAsync ()
        {
            TimeEntryData data = null;
            var user = ServiceContainer.Resolve<AuthManager> ().User;
            if (user == null)
                return null;

            // We're already loading draft data, wait for it to load, no need to create several drafts
            if (draftDataTCS != null) {
                data = await draftDataTCS.Task;
                if (data == null)
                    return null;
                data = new TimeEntryData (data);
                return new TimeEntryModel (data);
            }

            draftDataTCS = new TaskCompletionSource<TimeEntryData> ();

            try {
                var store = ServiceContainer.Resolve<IDataStore> ();

                if (user.DefaultWorkspaceId == Guid.Empty) {
                    // User data has not yet been loaded by AuthManager, duplicate the effort and load ourselves:
                    var userRows = await store.Table<UserData> ()
                        .Take (1).QueryAsync (m => m.Id == user.Id);
                    user = userRows.First ();
                }

                var rows = await store.Table<TimeEntryData> ()
                    .Where (m => m.State == TimeEntryState.New && m.DeletedAt == null && m.UserId == user.Id)
                    .OrderBy (m => m.ModifiedAt)
                    .Take (1).QueryAsync ();
                data = rows.FirstOrDefault ();

                if (data == null) {
                    // Create new draft object
                    var newData = new TimeEntryData () {
                        State = TimeEntryState.New,
                        UserId = user.Id,
                        WorkspaceId = user.DefaultWorkspaceId,
                        DurationOnly = user.TrackingMode == TrackingMode.Continue,
                    };
                    MarkDirty (newData);

                    await store.ExecuteInTransactionAsync (ctx => {
                        newData = ctx.Put (newData);
                        if (ShouldAddDefaultTag) {
                            AddDefaultTags (ctx, newData.WorkspaceId, newData.Id);
                        }
                    });

                    data = newData;
                }
            } catch (Exception ex) {
                var log = ServiceContainer.Resolve<Logger> ();
                log.Warning (Tag, ex, "Failed to retrieve/create draft.");
            } finally {
                draftDataTCS.SetResult (data);
                draftDataTCS = null;
            }

            return new TimeEntryModel (data);
        }

        public static async Task<TimeEntryModel> CreateFinishedAsync (TimeSpan duration)
        {
            var user = ServiceContainer.Resolve<AuthManager> ().User;
            if (user == null)
                return null;

            var store = ServiceContainer.Resolve<IDataStore> ();
            var now = Time.UtcNow;

            var newData = new TimeEntryData () {
                State = TimeEntryState.Finished,
                StartTime = now - duration,
                StopTime = now,
                UserId = user.Id,
                WorkspaceId = user.DefaultWorkspaceId,
                DurationOnly = user.TrackingMode == TrackingMode.Continue,
            };
            MarkDirty (newData);

            await store.ExecuteInTransactionAsync (ctx => {
                newData = ctx.Put (newData);
                if (ShouldAddDefaultTag) {
                    AddDefaultTags (ctx, newData.WorkspaceId, newData.Id);
                }
            });

            return new TimeEntryModel (newData);
        }

        public static explicit operator TimeEntryModel (TimeEntryData data)
        {
            if (data == null)
                return null;
            return new TimeEntryModel (data);
        }

        public static implicit operator TimeEntryData (TimeEntryModel model)
        {
            return model.Data;
        }
    }
}
