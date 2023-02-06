
using System.Data;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using Stimulsoft.Report;
using Stimulsoft.Report.Angular;
using Stimulsoft.Report.Components.Table;
using Stimulsoft.Report.Mvc;
using Stimulsoft.Report.Web;

namespace StimulSoftEmbeded.ReportServices
{
    public  class ReportBuilder
    {

        private readonly Func<Task<DataSet>> _fetchDataDelegate;
        private readonly string _reportPath;
        private readonly Controller _controller;
        public ReportBuilder(Func<Task<DataSet>> func , string reportPath , Controller controller)
        {
            _fetchDataDelegate = func;
            _reportPath = reportPath;
            _controller = controller;
        }

        protected ReportBuilder(string reportPath, Controller controller)
        {
            _reportPath = reportPath;
            _controller = controller;

        }

        public virtual IActionResult InitViewer()
        {
            var requestParams = StiNetCoreViewer.GetRequestParams(_controller);
            return StiAngularViewer.ViewerDataResult(requestParams, GetViewOptions());
        }

        protected static StiAngularViewerOptions GetViewOptions() 
            => new()
            {
            Server =
            {
                RequestTimeout = 300
            },
            Actions =
            {
                ViewerEvent = nameof(ViewEvent)
            }
        };

        public  async Task<IActionResult?> ViewEvent()
        {
            var requestParams = StiNetCoreViewer.GetRequestParams(_controller);
            return requestParams.Action == StiAction.GetReport ? await GetReport() 
                : StiNetCoreViewer.ProcessRequestResult(_controller);

        }

        protected   async Task<ActionResult?> GetReport( )
        {
            var report = CreateNewReport();
            var prop = GetData();
            object dtoFilter = prop.Item1;
            report = SetReportVariables(report, prop.Item2);
            var dataSet = await ExecuteFetchData();
            report.RegData("anaaam", dataSet);
            await report.Dictionary.SynchronizeAsync();
            return StiNetCoreViewer.GetReportResult(_controller, report);
        }

        protected virtual Task<DataSet> ExecuteFetchData() => _fetchDataDelegate();
        private StiReport CreateNewReport()
        {
            var report = StiReport.CreateNewReport();
            var path = StiAngularHelper.MapPath(_controller, _reportPath);
            report.Load(path).Compile();
            return report;
        }

        private static StiReport SetReportVariables(StiReport? report, Dictionary<string, string>? dicVars)
        {
            if (dicVars == null || report == null) return report;

            foreach (var item in dicVars)
            {
                var val = item.Value;
                if (string.IsNullOrEmpty(val)) continue;
                if (!report.Dictionary.Variables.Contains(item.Key)) continue;
                if (IsBase64(val))
                    val = UrlDecodeBase64(val);

                if (item.Key.Contains("Date"))
                {

                    report[item.Key] = !string.IsNullOrEmpty(val) ? DateTime.Parse(val).ToString("dd/MM/yyyy") : val;

                }
                else
                    report[item.Key] = val;

            }
            return report;
        }

        public static string UrlDecodeBase64( string str)
        {
            string decoded = "";
            try
            {
                decoded = System.Text.Encoding.UTF8.GetString(System.Web.HttpUtility.UrlDecodeToBytes(System.Convert.FromBase64String(str)));

            }
            catch { }
            return decoded;
        }
        protected static bool IsBase64( string str)
        {
            if ((str?.Length % 4) != 0)
            {
                return false;
            }

            //decode - encode and compare
            try
            {
                string decoded = System.Text.Encoding.UTF8.GetString(System.Convert.FromBase64String(str));
                string encoded = System.Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(decoded));
                if (str.Equals(encoded, StringComparison.InvariantCultureIgnoreCase))
                {
                    return true;
                }
            }
            catch
            {
                return false;
            }

            return false;
        }
        private (object, Dictionary<string , string>) GetData()
        {
            var httpContext = new Stimulsoft.System.Web.HttpContext(_controller.HttpContext);
            var properties = httpContext.Request.Params["properties"]?.ToString();
            var data = Convert.FromBase64String(properties);
            var json = Encoding.UTF8.GetString(data);
            return (JsonConvert.DeserializeObject<object>(json),
                JsonConvert.DeserializeObject<Dictionary<string, string>>(json));

        }
    }

   

    public class ReportBuilder<TDto> : ReportBuilder
    {
        private readonly Func<TDto, Task<DataSet>> _fetchDataDelegate;
        private readonly TDto _dto;
        public ReportBuilder(Func<TDto, Task<DataSet>> func, string reportPath, Controller controller, TDto dto)
        :base(reportPath , controller)
        {
            _fetchDataDelegate = func;
            this._dto = dto;
        }

        protected override  Task<DataSet> ExecuteFetchData() => _fetchDataDelegate(_dto);

    }
}