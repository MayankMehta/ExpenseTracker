using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Web;

namespace ExpenseTracker.WebClient.Helpers
{
    // Helper class responsible for initializing the client
    public static class ExpenseTrackerHttpClient
    {
        public static HttpClient GetClient(string requestedVersion = null)
        {
            HttpClient client = new HttpClient();
            client.BaseAddress = new Uri(ExpenseTrackerConstants.ExpenseTrackerAPI);
            // Stating that we want our response in json format and nothing else
            client.DefaultRequestHeaders.Accept.Clear();
            client.DefaultRequestHeaders.Accept.Add(
                new MediaTypeWithQualityHeaderValue("application/json"));

            if (requestedVersion != null)
            {
                // Versioning: Through a custom request header
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.expensetrackerapi.v"
                    + requestedVersion + "+json"));
            }

            return client;
        }
    }
}