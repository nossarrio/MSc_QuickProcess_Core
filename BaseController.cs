using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace QuickProcess
{
    [ApiController]
    [Route("[controller]")]
    public  class BaseController : Controller
    {
        public ContentResult Ok(dynamic result)
        {
            return Content(serialize(new
            {
                ResponseCode = "00",
                ResponseDescription = "okay",
                Result = serialize(result)
            }));
        }

        public ContentResult Error(string message, string exception)
        {
            if (Microsoft.Extensions.Hosting.EnvironmentName.Development.ToLower() == "development")
            {
                message = message + " " + exception;
            }

            return Content(serialize(new
            {
                ResponseCode = "-1",
                ResponseDescription = message
            }));
        }

        private string serialize(dynamic obj)
        {
            return Newtonsoft.Json.JsonConvert.SerializeObject(obj);
        }

        private T deserialize<T>(string obj)
        {
            return Newtonsoft.Json.JsonConvert.DeserializeObject<T>(obj);
        }
    }
}
