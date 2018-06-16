using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using Prometheus.Client.Collectors;

namespace Prometheus.Client.MetricPusher
{
    public class MetricPusher
    {
        private const string _contentType = "text/plain; version=0.0.4";

        protected MetricPusher()
        {
        }

        /// <summary>
        ///     Push metrics to single pushgateway endpoint
        /// </summary>
        /// <param name="endpoint">PushGateway endpoint</param>
        /// <param name="job">job name</param>
        /// <param name="instance">instance</param>
        /// <param name="contentType">Content-Type</param>
        /// <returns></returns>
        public static async Task PushAsync(string endpoint, string job, string instance, string contentType)
        {
            await PushAsync(new[] { endpoint }, job, instance, contentType).ConfigureAwait(false);
        }

        /// <summary>
        ///     Push metrics to multiple pushgateway endpoints (fault-tolerance)
        /// </summary>
        /// <param name="endpoints">multiple pushgateway enpoints (fault-tolerance)</param>
        /// <param name="job">job name</param>
        /// <param name="instance">instance name</param>
        /// <param name="contentType">content-type</param>
        /// <returns></returns>
        public static async Task PushAsync(string[] endpoints, string job, string instance, string contentType)
        {
            string cntType = null;
            if (string.IsNullOrEmpty(contentType))
                cntType = _contentType;

            if (string.IsNullOrEmpty(job))
                throw new ArgumentNullException(nameof(job));

            var metrics = CollectorRegistry.Instance.CollectAll();
            var memoryStream = new MemoryStream();
            ScrapeHandler.ProcessScrapeRequest(metrics, cntType, memoryStream);
            memoryStream.Position = 0;
            var streamContent = new StreamContent(memoryStream);

            var httpClient = new HttpClient();
            var tasks = new List<Task<HttpResponseMessage>>(endpoints.Length);

            foreach (string endpoint in endpoints)
            {
                if (string.IsNullOrEmpty(endpoint))
                    throw new ArgumentNullException(nameof(endpoint));

                string url = $"{endpoint.TrimEnd('/')}/job/{job}";
                if (!string.IsNullOrEmpty(instance))
                    url = $"{url}/instance/{instance}";

                if (!Uri.TryCreate(url, UriKind.Absolute, out var targetUrl))
                    throw new ArgumentException("Endpoint must be a valid url", nameof(endpoint));

                tasks.Add(httpClient.PostAsync(targetUrl, streamContent));
            }

            await Task.WhenAll(tasks).ConfigureAwait(false);

            foreach (var task in tasks)
            {
                var response = await task.ConfigureAwait(false);
                response.EnsureSuccessStatusCode();
            }
        }
    }
}