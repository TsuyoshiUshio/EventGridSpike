
using System.IO;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Azure.WebJobs.Host;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Microsoft.Azure.WebJobs.Extensions.EventGrid;
using System.Threading.Tasks;
using Microsoft.Azure.EventGrid.Models;
using System;
using System.Net.Http;

namespace EventGridSpike
{
    public static class Function1
    {
        public static HttpClient client;
        static Function1() {
            client = new HttpClient();
            client.DefaultRequestHeaders.Add("aeg-sas-key", System.Environment.GetEnvironmentVariable("EventGridSasKey"));
        }

        public class OrchestratorEvent
        {
            public const string STARTED = "Started";
            public const string COMPLETED = "Completed";
            public string InstanceId { get; set; }
            public string EventType { get; set; }
            public DateTime EventTime { get; set; }
        }

        [FunctionName("Sender")]
        public static async Task<IActionResult> Sender([HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)]HttpRequest req, TraceWriter log)
        {
            log.Info("Send message to the Topic!");
            var newId = Guid.NewGuid().ToString();
            var events = new EventGridEvent[]
            {
                new EventGridEvent
                {
                    Id = newId,
                    EventType = "orchestratorEvent",
                    Subject = $"durable/orchestrator/{newId}",
                    EventTime = new DateTime(2018, 3, 10, 10,10, 10,DateTimeKind.Local),
                    Data = new OrchestratorEvent
                    {
                        InstanceId = Guid.NewGuid().ToString(),
                        EventType = OrchestratorEvent.COMPLETED,
                        EventTime = new DateTime(2018, 3, 10, 10, 10, 10, DateTimeKind.Local)
                    },
                    DataVersion = "1.0"
                }
            };
            var resp = await client.PostAsJsonAsync<EventGridEvent[]>(System.Environment.GetEnvironmentVariable("EventGridTopicUrl"), events);

            return (ActionResult)new OkObjectResult($"Sucessfully sent");
        }

        // This dosen't work Japan and SouthEast Asia resion. 
        //[FunctionName("Subscriber")]
        //public static void Run([EventGridTrigger]JsonObjectAttribute eventGridEvent, TraceWriter log)
        //{
        //    log.Info(eventGridEvent.ToString());
        //}

        [FunctionName("HttpTriggerFunc")]
        public static IActionResult HttpTriggerFunc(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post")]HttpRequest req, TraceWriter log)
        {
            log.Info("C# HttpTrigger for EventGrid.");
            var requestBody = new StreamReader(req.Body).ReadToEnd();
            var messages = JsonConvert.DeserializeObject<JArray>(requestBody);
            // If the request is for subscription validation, send back the validation code.
            if (messages.Count > 0 && string.Equals((string)messages[0]["eventType"],
                "Microsoft.EventGrid.SubscriptionValidationEvent",
                System.StringComparison.OrdinalIgnoreCase))
            {
                log.Info("Validate request received");
                return new OkObjectResult(
                    new
                    {
                        validationResponse = messages[0]["data"]["validationCode"]
                    });
            }

            // The request is not for subscription validation, so it's for one or more events.
            foreach (JObject message in messages)
            {
                // Handle one event.
                EventGridEvent eventGridEvent = message.ToObject<EventGridEvent>();
                log.Info($"Subject: {eventGridEvent.Subject}");
                log.Info($"Time: {eventGridEvent.EventTime}");
                log.Info($"Event data: {eventGridEvent.Data.ToString()}");
            }

            return new OkObjectResult("");


        }
    }
}
