using GizmoDALV2;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace ServerService
{
    [Export(typeof(IUserTimeBillingHandler))]
    public class UserBillingPlugin : GizmoServiceModulePluginBase,
        IUserTimeBillingHandler
    {
        #region FIELDS
        private double seconds = 180;
        private object CFG_FILE_LOCK = new object();
        private BillingPluginConfig config;
        #endregion

        public bool PreBillSession(int userId,
            double spanSeconds,
            bool isNegativeAllowed,
            decimal creditLimit,
            int userSessionId,
            int? hostGroupId,
            int? userGroupId,
            int? billProfileId,
            DateTime currentTime,
            IGizmoDBContext cx,
            out bool logout)
        {
            logout = false;

            var userTime = this.GetUserTimes(userId, cx).Single();

            int maxDialy = config.DailyLimit;
            int maxWeekly = config.WeeklyLimit;

            if (userTime.Value < 60)
                logout = true;

            //since user balance changed notify
            this.Service.ScheduleUserBalanceEvent(userId);

            seconds -= 60;

            //indicate that normal billing procedure should not occur
            return true;
        }

        public bool PreBalanceHandle(int? userId, int? hostGroupId, IGizmoDBContext cx, out Dictionary<int, UserBalance> currentState)
        {
            if (cx == null)
                throw new ArgumentNullException(nameof(cx));

            currentState = null;

            //we will not handle pre balance pass which means the normal user balances will be calculated
            return false;
        }

        public void PostBalanceHandle(int? userId, int? hostGroupId, Dictionary<int, UserBalance> currentState, IGizmoDBContext cx)
        {
            if (cx == null)
                throw new ArgumentNullException(nameof(cx));

            if (currentState == null)
                throw new ArgumentNullException(nameof(currentState));

            var userTimes = this.GetUserTimes(userId, cx);

            foreach (var userBalance in currentState)
            {
                userBalance.Value.AvailableTime = userTimes[userBalance.Key];
                userBalance.Value.AvailableCreditedTime = userTimes[userBalance.Key];
            }
        }

        private Dictionary<int, double> GetUserTimes(int? userId, IGizmoDBContext cx)
        {
            if (cx == null)
                throw new ArgumentNullException(nameof(cx));
            
            var query = cx.QueryableSet<GizmoDALV2.Entities.UserMember>();

            if (userId.HasValue)
            {
                query = query.Where(x => x.Id == userId.Value);
            }

            return query.Select(x => new { x.Id }).ToDictionary(x => x.Id, y => seconds);
        }

        public override void Initialize()
        {
            base.Initialize();

            //get any required configuration here
            string CONFIG_FILE_NAME = Path.Combine(Environment.CurrentDirectory, "BillingPlugin.json");
            if (File.Exists(CONFIG_FILE_NAME))
            {
                lock (CFG_FILE_LOCK)
                {
                    //try to obtain configuration
                    using (var stream = new FileStream(CONFIG_FILE_NAME, FileMode.Open, FileAccess.Read))
                    {
                        using (StreamReader reader = new StreamReader(stream))
                        using (var jsonReader = new JsonTextReader(reader))
                        {
                            JsonSerializer ser = new JsonSerializer()
                            {
                                MissingMemberHandling = MissingMemberHandling.Ignore,
                                NullValueHandling = NullValueHandling.Ignore,
                                DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate
                            };
                            config = ser.Deserialize<BillingPluginConfig>(jsonReader);
                        }
                    }
                }
            }
            else
            {
                config = new BillingPluginConfig
                {
                    DayEnd = new DayTime() { Hour = 18, Minute = 0 },
                    WeekStartDay = DayOfWeek.Monday,
                };

                lock (CFG_FILE_LOCK)
                {
                    using (var stream = new FileStream(CONFIG_FILE_NAME, FileMode.OpenOrCreate, FileAccess.ReadWrite))
                    {
                        stream.SetLength(0);
                        using (StreamWriter writer = new StreamWriter(stream))
                        using (var jsonWriter = new JsonTextWriter(writer))
                        {
                            JsonSerializer ser = new JsonSerializer()
                            {
                                Formatting = Formatting.Indented,
                                DefaultValueHandling = DefaultValueHandling.Include,
                                NullValueHandling = NullValueHandling.Include
                            };
                            ser.Serialize(jsonWriter, config);
                        }
                    }
                }
            }
        }
    }


    [DataContract()]
    public class BillingPluginConfig
    {
        #region PROPERTIES
        
        /// <summary>
        /// Gets or sets maximum of daily minutes.
        /// </summary>
        [DataMember()]
        public int DailyLimit
        {
            get; set;
        }

        /// <summary>
        /// Gets or sets maximum weekly limit.
        /// </summary>
        [DataMember()]
        public int WeeklyLimit
        {
            get;set;
        }

        /// <summary>
        /// Gets or sets day start.
        /// </summary>
        [DataMember()]
        public DayTime DayEnd
        {
            get; set;
        }

        /// <summary>
        /// Gets or sets day end.
        /// </summary>
        [DataMember()]
        public DayOfWeek WeekStartDay
        {
            get; set;
        } 

        #endregion
    }

    [DataContract()]
    public class DayTime
    {
        #region PROPERTIES
        
        /// <summary>
        /// Gets or sets hour.
        /// </summary>
        [DataMember()]
        public int Hour
        {
            get; set;
        }

        /// <summary>
        /// Gets or sets minute.
        /// </summary>
        [DataMember()]
        public int Minute
        {
            get; set;
        } 

        #endregion
    }
}

