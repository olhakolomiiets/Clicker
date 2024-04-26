using System.Collections.Generic;

namespace CBS
{
    public static class AzureQueues
    {
        // battle pass
        public const string BattlePassPostProcessQueue = "BattlePassPostProcess";

        // events
        public const string StartCBSEventQueue = "StartCBSEvent";
        public const string StopCBSEventQueue = "StopCBSEvent";
        public const string ExecuteCBSEventQueue = "ExecuteCBSEvent";

        public const string BattlePassQueueName = "cbs-battlepass-queue";
        public const string StartEventQueueName = "cbs-start-event-queue";
        public const string StopEventQueueName = "cbs-stop-event-queue";
        public const string ExecuteEventQueueName = "cbs-execute-event-queue";

        public static List<string> AllQueues
        {
            get
            {
                return new List<string>
                {
                    BattlePassPostProcessQueue,
                    StartCBSEventQueue,
                    StopCBSEventQueue,
                    ExecuteCBSEventQueue
                };
            }
        }

        public static string GetQueueContainerName(string queue)
        {
            if (queue == BattlePassPostProcessQueue) return BattlePassQueueName;
            if (queue == StartCBSEventQueue) return StartEventQueueName;
            if (queue == StopCBSEventQueue) return StopEventQueueName;
            if (queue == ExecuteCBSEventQueue) return ExecuteEventQueueName;
            return string.Empty;
        }
    }
}
