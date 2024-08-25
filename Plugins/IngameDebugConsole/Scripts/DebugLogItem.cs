using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Text;
using UnityEngine.Networking;
using System.Collections;
using System;
using System.IO;
using System.Collections.Generic;
using UnityGoogleDrive;



#if UNITY_EDITOR
using UnityEditor;
using System.Text.RegularExpressions;
#endif

// A UI element to show information about a debug entry
namespace IngameDebugConsole
{
    public class DebugLogItem : MonoBehaviour, IPointerClickHandler
    {
        #region Platform Specific Elements
#if !UNITY_2018_1_OR_NEWER
#if !UNITY_EDITOR && UNITY_ANDROID
		private static AndroidJavaClass m_ajc = null;
		private static AndroidJavaClass AJC
		{
			get
			{
				if( m_ajc == null )
					m_ajc = new AndroidJavaClass( "com.yasirkula.unity.DebugConsole" );

				return m_ajc;
			}
		}

		private static AndroidJavaObject m_context = null;
		private static AndroidJavaObject Context
		{
			get
			{
				if( m_context == null )
				{
					using( AndroidJavaObject unityClass = new AndroidJavaClass( "com.unity3d.player.UnityPlayer" ) )
					{
						m_context = unityClass.GetStatic<AndroidJavaObject>( "currentActivity" );
					}
				}

				return m_context;
			}
		}
#elif !UNITY_EDITOR && UNITY_IOS
		[System.Runtime.InteropServices.DllImport( "__Internal" )]
		private static extern void _DebugConsole_CopyText( string text );
#endif
#endif
        #endregion

#pragma warning disable 0649
        // Cached components
        [SerializeField]
        private RectTransform transformComponent;
        public RectTransform Transform { get { return transformComponent; } }

        [SerializeField]
        private Image imageComponent;
        public Image Image { get { return imageComponent; } }

        [SerializeField]
        private CanvasGroup canvasGroupComponent;
        public CanvasGroup CanvasGroup { get { return canvasGroupComponent; } }

        [SerializeField]
        private Text logText;
        [SerializeField]
        private Image logTypeImage;

        // Objects related to the collapsed count of the debug entry
        [SerializeField]
        private GameObject logCountParent;
        [SerializeField]
        private Text logCountText;

        [SerializeField]
        private RectTransform copyLogButton;

        [SerializeField]
        private RectTransform reportLogButton;
#pragma warning restore 0649

        // Debug entry to show with this log item
        private DebugLogEntry logEntry;
        public DebugLogEntry Entry { get { return logEntry; } }

        private DebugLogEntryTimestamp? logEntryTimestamp;
        public DebugLogEntryTimestamp? Timestamp { get { return logEntryTimestamp; } }

        // Index of the entry in the list of entries
        [System.NonSerialized] public int Index;

        private bool isExpanded;
        public bool Expanded { get { return isExpanded; } }

        private Vector2 logTextOriginalPosition;
        private Vector2 logTextOriginalSize;
        private float copyLogButtonHeight;
        private float reportLogButtonHeight;

        private DebugLogRecycledListView listView;

        public Button ReportBugButton;

        [HideInInspector]
        public string WebhookURL = "";
        [HideInInspector]
        public string GoogleDriveFolderIdLogs = "";
        [HideInInspector]
        public string GoogleDriveFolderIdScreenshots = "";
        [HideInInspector]
        public bool IsCapture = false;

        public void Initialize(DebugLogRecycledListView listView)
        {
            this.listView = listView;

            logTextOriginalPosition = logText.rectTransform.anchoredPosition;
            logTextOriginalSize = logText.rectTransform.sizeDelta;
            copyLogButtonHeight = copyLogButton.anchoredPosition.y + copyLogButton.sizeDelta.y + 2f; // 2f: space between text and button
            reportLogButtonHeight = reportLogButton.anchoredPosition.y + reportLogButton.sizeDelta.y + 2f; // 2f: space between text and button

#if !UNITY_EDITOR && UNITY_WEBGL
			copyLogButton.gameObject.AddComponent<DebugLogItemCopyWebGL>().Initialize( this );
#endif
        }

        public void SetContent(DebugLogEntry logEntry, DebugLogEntryTimestamp? logEntryTimestamp, int entryIndex, bool isExpanded)
        {
            this.logEntry = logEntry;
            this.logEntryTimestamp = logEntryTimestamp;
            this.Index = entryIndex;
            this.isExpanded = isExpanded;

            Vector2 size = transformComponent.sizeDelta;
            if (isExpanded)
            {
                logText.horizontalOverflow = HorizontalWrapMode.Wrap;
                size.y = listView.SelectedItemHeight;

                if (!copyLogButton.gameObject.activeSelf)
                {
                    copyLogButton.gameObject.SetActive(true);

                    logText.rectTransform.anchoredPosition = new Vector2(logTextOriginalPosition.x, logTextOriginalPosition.y + copyLogButtonHeight * 0.5f);
                    logText.rectTransform.sizeDelta = logTextOriginalSize - new Vector2(0f, copyLogButtonHeight);
                }

                if (!reportLogButton.gameObject.activeSelf)
                {
                    reportLogButton.gameObject.SetActive(true);

                    //logText.rectTransform.anchoredPosition = new Vector2(logTextOriginalPosition.x, logTextOriginalPosition.y + reportLogButtonHeight * 0.5f);
                    //logText.rectTransform.sizeDelta = logTextOriginalSize - new Vector2(0f, reportLogButtonHeight);
                }
            }
            else
            {
                logText.horizontalOverflow = HorizontalWrapMode.Overflow;
                size.y = listView.ItemHeight;

                if (copyLogButton.gameObject.activeSelf)
                {
                    copyLogButton.gameObject.SetActive(false);

                    logText.rectTransform.anchoredPosition = logTextOriginalPosition;
                    logText.rectTransform.sizeDelta = logTextOriginalSize;
                }

                if (reportLogButton.gameObject.activeSelf)
                {
                    reportLogButton.gameObject.SetActive(false);

                    //logText.rectTransform.anchoredPosition = new Vector2(logTextOriginalPosition.x, logTextOriginalPosition.y + reportLogButtonHeight * 0.5f);
                    //logText.rectTransform.sizeDelta = logTextOriginalSize - new Vector2(0f, reportLogButtonHeight);
                }
            }

            transformComponent.sizeDelta = size;

            SetText(logEntry, logEntryTimestamp, isExpanded);
            logTypeImage.sprite = logEntry.logTypeSpriteRepresentation;
        }

        // Show the collapsed count of the debug entry
        public void ShowCount()
        {
            logCountText.text = logEntry.count.ToString();

            if (!logCountParent.activeSelf)
                logCountParent.SetActive(true);
        }

        // Hide the collapsed count of the debug entry
        public void HideCount()
        {
            if (logCountParent.activeSelf)
                logCountParent.SetActive(false);
        }

        // Update the debug entry's displayed timestamp
        public void UpdateTimestamp(DebugLogEntryTimestamp timestamp)
        {
            logEntryTimestamp = timestamp;

            if (isExpanded || listView.manager.alwaysDisplayTimestamps)
                SetText(logEntry, timestamp, isExpanded);
        }

        private void SetText(DebugLogEntry logEntry, DebugLogEntryTimestamp? logEntryTimestamp, bool isExpanded)
        {
            if (!logEntryTimestamp.HasValue || (!isExpanded && !listView.manager.alwaysDisplayTimestamps))
                logText.text = isExpanded ? logEntry.ToString() : logEntry.logString;
            else
            {
                StringBuilder sb = listView.manager.sharedStringBuilder;
                sb.Length = 0;

                if (isExpanded)
                {
                    logEntryTimestamp.Value.AppendFullTimestamp(sb);
                    sb.Append(": ").Append(logEntry.ToString());
                }
                else
                {
                    logEntryTimestamp.Value.AppendTime(sb);
                    sb.Append(" ").Append(logEntry.logString);
                }

                logText.text = sb.ToString();
            }
        }

        // This log item is clicked, show the debug entry's stack trace
        public void OnPointerClick(PointerEventData eventData)
        {
#if UNITY_EDITOR
            if (eventData.button == PointerEventData.InputButton.Right)
            {
                Match regex = Regex.Match(logEntry.stackTrace, @"\(at .*\.cs:[0-9]+\)$", RegexOptions.Multiline);
                if (regex.Success)
                {
                    string line = logEntry.stackTrace.Substring(regex.Index + 4, regex.Length - 5);
                    int lineSeparator = line.IndexOf(':');
                    MonoScript script = AssetDatabase.LoadAssetAtPath<MonoScript>(line.Substring(0, lineSeparator));
                    if (script != null)
                        AssetDatabase.OpenAsset(script, int.Parse(line.Substring(lineSeparator + 1)));
                }
            }
            else
                listView.OnLogItemClicked(this);
#else
			listView.OnLogItemClicked( this );
#endif
        }

        public void CopyLog()
        {
#if UNITY_EDITOR || !UNITY_WEBGL
            string log = GetCopyContent();
            if (string.IsNullOrEmpty(log))
                return;

#if UNITY_EDITOR || UNITY_2018_1_OR_NEWER || (!UNITY_ANDROID && !UNITY_IOS)
            GUIUtility.systemCopyBuffer = log;
#elif UNITY_ANDROID
			AJC.CallStatic( "CopyText", Context, log );
#elif UNITY_IOS
			_DebugConsole_CopyText( log );
#endif
#endif
        }

        // Report Bug Button
        public void ReportBug(string bugDescription, string platform, string stepsToReproduce, string expectedBehavior, bool isCapture)
        {
            if (!string.IsNullOrEmpty(GoogleDriveFolderIdLogs) && !string.IsNullOrEmpty(GoogleDriveFolderIdScreenshots))
            {
                IsCapture = isCapture;
                StartCoroutine(CaptureAndReportBug(bugDescription, platform, stepsToReproduce, expectedBehavior));
            }
            else
            {
                Debug.LogError("Can't report bug, Please set google drive settings.");
            }
        }

        private IEnumerator CaptureAndReportBug(string bugDescription, string platform, string stepsToReproduce, string expectedBehavior)
        {
            string logPath = Path.Combine(Application.persistentDataPath, "log.txt");
            File.WriteAllText(logPath, logEntry.ToString());

            // Upload log to Google Drive
            Guid newGuidLog = Guid.NewGuid();
            string logLink = null;
            string logName = newGuidLog.ToString() + ".txt";
            yield return StartCoroutine(UploadFileToGoogleDrive(logName, GoogleDriveFolderIdLogs, logPath, link => logLink = link));

            if (IsCapture)
            {
                // Capture screenshot
                string screenshotPath = Path.Combine(Application.persistentDataPath, "screenshot.png");
                ScreenCapture.CaptureScreenshot(screenshotPath);

                // Wait for the screenshot to be saved
                yield return new WaitForSeconds(1f);

                // Upload screenshot to Google Drive
                Guid newGuidScreenshot = Guid.NewGuid();
                string screenshotLink = null;
                string screenshotName = newGuidScreenshot.ToString() + ".png";
                yield return StartCoroutine(UploadFileToGoogleDrive(screenshotName, GoogleDriveFolderIdScreenshots, screenshotPath, link => screenshotLink = link));

                // Send the bug report with the screenshot link
                yield return StartCoroutine(SendBugReport(bugDescription, platform, stepsToReproduce, expectedBehavior, logLink, screenshotLink));

                // Clean up the file
                File.Delete(logPath);
                File.Delete(screenshotPath);
            }
            else
            {
                // Send the bug report without the screenshot
                yield return StartCoroutine(SendBugReport(bugDescription, platform, stepsToReproduce, expectedBehavior, logLink, null));

                // Clean up the file
                File.Delete(logPath);
            }
        }

        private IEnumerator UploadFileToGoogleDrive(string fileName, string folderId, string filePath, System.Action<string> onComplete)
        {
            byte[] fileData = File.ReadAllBytes(filePath);

            var file = new UnityGoogleDrive.Data.File
            {
                Name = fileName,
                Content = fileData,
                Parents = new List<string> { folderId } // Specify the folder ID here
            };

            var request = GoogleDriveFiles.Create(file).Send();

            yield return request;

            if (request.GoogleDriveRequest.IsError)
            {
                Debug.LogError("Error uploading file: " + request.GoogleDriveRequest.Error);
                onComplete?.Invoke(null);
            }
            else
            {
                Debug.Log("File uploaded to Google Drive successfully!");
                string link = request.GoogleDriveRequest.ResponseData.Id; // Process the response to extract the file ID
                onComplete("https://drive.google.com/file/d/" + link + "/view");
            }
        }


        private IEnumerator SendBugReport(string bugDescription, string platform, string stepsToReproduce, string expectedBehavior, string logLink, string screenshotLink)
        {
            string dateTime = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " (UTC +7)";
            string reporterName = "Nagasuki"; // Replace with the actual reporter name

            string formattedDescription =
                "📖 **Description:**\n" + bugDescription + "\n\n" +
                "🕹️ **Platform:**\n" + platform + "\n\n" +
                "🎯 **Steps to Reproduce:**\n" + stepsToReproduce + "\n" +
                "📌 **Expected Behavior:**\n" + expectedBehavior + "\n\n";

            string jsonPayload = "";

            if (IsCapture)
            {
                jsonPayload = $@"
        {{
            ""content"": null,
            ""embeds"": [
                {{
                    ""title"": ""🐞 Bug Report"",
                    ""description"": ""{EscapeJson(formattedDescription)}"",
                    ""color"": 14177041,
                    ""fields"": [
                        {{
                            ""name"": ""📅 Date & Time"",
                            ""value"": ""{dateTime}"",
                            ""inline"": false
                        }},
                        {{
                            ""name"": ""📜 Log Exeption"",
                            ""value"": ""[Log.txt]({EscapeJson(logLink)})"",
                            ""inline"": false
                        }},
                        {{
                            ""name"": ""📷 Screenshot"",
                            ""value"": ""[Screenshot.png]({EscapeJson(screenshotLink)})"",
                            ""inline"": false
                        }}
                    ],
                    ""footer"": {{
                        ""text"": ""Reported by: {EscapeJson(reporterName)}""
                    }}
                }}
            ]
        }}";
            }
            else
            {
                jsonPayload = $@"
        {{
            ""content"": null,
            ""embeds"": [
                {{
                    ""title"": ""🐞 Bug Report"",
                    ""description"": ""{EscapeJson(formattedDescription)}"",
                    ""color"": 14177041,
                    ""fields"": [
                        {{
                            ""name"": ""📅 Date & Time"",
                            ""value"": ""{dateTime}"",
                            ""inline"": false
                        }},
                        {{
                            ""name"": ""📜 Log Exeption"",
                            ""value"": ""[Log.txt]({EscapeJson(logLink)})"",
                            ""inline"": false
                        }}
                    ],
                    ""footer"": {{
                        ""text"": ""Reported by: {EscapeJson(reporterName)}""
                    }}
                }}
            ]
        }}";
            }

            //Debug.Log("JSON Payload: " + jsonPayload); // Print JSON to debug

            using (UnityWebRequest www = new UnityWebRequest(WebhookURL, "POST"))
            {
                byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(jsonPayload);
                www.uploadHandler = new UploadHandlerRaw(bodyRaw);
                www.downloadHandler = new DownloadHandlerBuffer();
                www.SetRequestHeader("Content-Type", "application/json");

                yield return www.SendWebRequest();

                if (www.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogError("Error reporting bug: " + www.error);
                }
                else
                {
                    Debug.Log("Bug reported successfully!");
                }
            }
        }

        private string EscapeJson(string value)
        {
            return value.Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "\\r");
        }

        internal string GetCopyContent()
        {
            if (!logEntryTimestamp.HasValue)
                return logEntry.ToString();
            else
            {
                StringBuilder sb = listView.manager.sharedStringBuilder;
                sb.Length = 0;

                logEntryTimestamp.Value.AppendFullTimestamp(sb);
                sb.Append(": ").Append(logEntry.ToString());

                return sb.ToString();
            }
        }

        public float CalculateExpandedHeight(DebugLogEntry logEntry, DebugLogEntryTimestamp? logEntryTimestamp)
        {
            string text = logText.text;
            HorizontalWrapMode wrapMode = logText.horizontalOverflow;

            SetText(logEntry, logEntryTimestamp, true);
            logText.horizontalOverflow = HorizontalWrapMode.Wrap;

            float result = logText.preferredHeight + copyLogButtonHeight + reportLogButtonHeight;


            logText.text = text;
            logText.horizontalOverflow = wrapMode;

            return Mathf.Max(listView.ItemHeight, result);
        }

        // Return a string containing complete information about the debug entry
        public override string ToString()
        {
            return logEntry.ToString();
        }
    }
}
