using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

/// <summary>
/// Summary description for Obsrvus
/// </summary>
public class Obsrvus {
	/// <summary>
	/// Key identifier for the application to log to.
	/// </summary>
	public string ApplicationKey { get; set; }

	/// <summary>
	/// Key identifier for the system to log to.
	/// </summary>
	public string SystemKey { get; set; }

	/// <summary>
	/// Whether or not to use Task for background logging.
	/// </summary>
	public bool UseTask { get; set; }

	/// <summary>
	/// Whether or not to throw an Exception if the web call fails.
	/// </summary>
	public bool ThrowExceptionIfLogFails { get; set; }

	/// <summary>
	/// Shows whether or not the background task us running.
	/// </summary>
	public bool TaskIsRunning {
		get { return taskIsRunning; }
	}

	/// <summary>
	/// Sets/gets whether or not the background task us running.
	/// </summary>
	private static bool taskIsRunning;

	/// <summary>
	/// A list of queued items for upload.
	/// </summary>
	private static List<QueueItem> queueItems = new List<QueueItem>();

	/// <summary>
	/// Init a new instance of the Obsrv.us logger.
	/// </summary>
	/// <param name="applicationKey">Key identifier for the application to log to.</param>
	/// <param name="systemKey">Key identifier for the system to log to.</param>
	/// <param name="useTask">Whether or not to use Task for background logging.</param>
	/// <param name="throwExceptionIfLogFails">Whether or not to throw an Exception if the web call fails.</param>
	public Obsrvus(string applicationKey, string systemKey, bool useTask = true, bool throwExceptionIfLogFails = false) {
		this.ApplicationKey = applicationKey;
		this.SystemKey = systemKey;
		this.UseTask = useTask;
		this.ThrowExceptionIfLogFails = throwExceptionIfLogFails;
	}

	/// <summary>
	/// Log payload to Obsrv.us.
	/// </summary>
	/// <param name="payload">The content to log.</param>
	public void Log(object payload) {
		Log(
			this.ApplicationKey,
			this.SystemKey,
			payload,
			this.UseTask,
			this.ThrowExceptionIfLogFails);
	}

	/// <summary>
	/// Log payload to Obsrv.us.
	/// </summary>
	/// <param name="applicationKey">Key identifier for the application to log to.</param>
	/// <param name="systemKey">Key identifier for the system to log to.</param>
	/// <param name="payload">The content to log.</param>
	/// <param name="useTask">Whether or not to use Task for background logging.</param>
	/// <param name="throwExceptionIfLogFails">Whether or not to throw an Exception if the web call fails.</param>
	public static void Log(string applicationKey, string systemKey, object payload, bool useTask = true, bool throwExceptionIfLogFails = false) {
		if (string.IsNullOrWhiteSpace(applicationKey))
			throw new ArgumentException("Cannot be null or blank.", "applicationKey");

		if (string.IsNullOrWhiteSpace(systemKey))
			throw new ArgumentException("Cannot be null or blank.", "systemKey");

		// No point in logging a null value.
		if (payload == null)
			return;

		// If we're using a task, queue up the item and spin up the task.
		if (useTask) {
			queueItems.Add(
				new QueueItem {
					ApplicationKey = applicationKey,
					SystemKey = systemKey,
					ThrowExceptionIfLogFails = throwExceptionIfLogFails,
					Payload = payload
				});

			if (!taskIsRunning) {
				Task.Run(delegate {
					LogQueue();
				});
			}

			return;
		}

		LogActual(
			applicationKey,
			systemKey,
			payload,
			throwExceptionIfLogFails);
	}

	/// <summary>
	/// Performs the actual web call to log to https://obsrv.us.
	/// </summary>
	/// <param name="applicationKey">Key identifier for the application to log to.</param>
	/// <param name="systemKey">Key identifier for the system to log to.</param>
	/// <param name="payload">The content to log.</param>
	/// <param name="throwExceptionIfLogFails">Whether or not to throw an Exception if the web call fails.</param>
	/// <returns>bool</returns>
	private static bool LogActual(string applicationKey, string systemKey, object payload, bool throwExceptionIfLogFails = false) {
		try {
			var request = WebRequest.Create("https://obsrv.us/api/v1/log") as HttpWebRequest;

			if (request == null) {
				if (throwExceptionIfLogFails)
					throw new WebException("Could not create WebRequest.");

				return false;
			}

			var json = JsonConvert.SerializeObject(payload);
			var data = Encoding.UTF8.GetBytes(json);

			request.ContentType = "application/json";
			request.ContentLength = data.Length;
			request.Method = "POST";

			request.Headers.Add("ApplicationKey", applicationKey);
			request.Headers.Add("SystemKey", systemKey);

			var stream = request.GetRequestStream();

			stream.Write(data, 0, data.Length);
			stream.Close();

			request.GetResponse();

			return true;
		}
		catch (Exception) {
			if (throwExceptionIfLogFails)
				throw;
		}

		return false;
	}

	/// <summary>
	/// Cycle through the queue and log entries.
	/// </summary>
	private static void LogQueue() {
		taskIsRunning = true;

		while (true) {
			var item = queueItems.FirstOrDefault(i => !i.Uploaded.HasValue);

			if (item == null)
				break;

			if (LogActual(
				item.ApplicationKey,
				item.SystemKey,
				item.Payload,
				item.ThrowExceptionIfLogFails))
				item.Uploaded = true;
		}

		// Remove all uploaded items.
		queueItems = queueItems
			.Where(i => !i.Uploaded.HasValue)
			.ToList();

		taskIsRunning = false;
	}

	/// <summary>
	/// A queue item.
	/// </summary>
	private class QueueItem {
		/// <summary>
		/// Key identifier for the application to log to.
		/// </summary>
		public string ApplicationKey { get; set; }

		/// <summary>
		/// Key identifier for the system to log to.
		/// </summary>
		public string SystemKey { get; set; }

		/// <summary>
		/// The content to log.
		/// </summary>
		public object Payload { get; set; }

		/// <summary>
		/// Whether or not to throw an Exception if the web call fails.
		/// </summary>
		public bool ThrowExceptionIfLogFails { get; set; }

		/// <summary>
		/// Whether or not the payload has been uploaded.
		/// </summary>
		public bool? Uploaded { get; set; }
	}
}