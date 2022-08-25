namespace QuickProcess
{
    public class QuickProcessBaseModel
    {
        public string? SessionId { get; set; }
        public GenericResponse? Response { get; set; }
        public string? AppId { get; set; }
        public string? Dform { get; set; }
        public string? ComponentName { get; set; }
        public string? parameters { get; set; }

        public dynamic getParameters()
        {
            dynamic parameters = new Newtonsoft.Json.Linq.JObject();
            var ObjParams = Newtonsoft.Json.Linq.JToken.Parse(this.parameters);
            Dictionary<string, string> dictObj = ObjParams.ToObject<Dictionary<string, string>>();
            foreach (var p in dictObj.Keys.ToArray())
            {
                if (dictObj[p] != null)
                {
                    parameters[p] = dictObj[p].ToString();
                }
            }
            return parameters;
        }

        public T getParameters<T>()
        {
            var obj= Newtonsoft.Json.JsonConvert.DeserializeObject<T>(parameters);
            return obj;
        }

        public async Task<UserSession> getSession()
        {
            return (await QuickProcess_Core.getSession(SessionId, ComponentName)).Item2;
        }       
    }
}
