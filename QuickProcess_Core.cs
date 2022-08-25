using Microsoft.AspNetCore.Mvc;
using QuickProcess;
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Net;
using System.Net.Http.Headers;
using System.Reflection;
using System.Runtime.Caching;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace QuickProcess
{
    public static class QuickProcess_Core
    {
        /// <summary>
        /// This method returns HTML representation of the default component of current application
        /// </summary>
        /// <param name="sessionId"></param>
        /// <returns></returns>
        public static async Task<String> getApp(string sessionId)
        {
            var app = await getApplication();
            string result = await getApplicationComponent(app.DefaultComponent, sessionId);
            return result;
        }


        /// <summary>
        /// This method returns an HTML representation of a specified component file. This is usally invoked through a browser url bar using an http get method
        /// </summary>
        /// <param name="ComponentName"></param>
        /// <param name="sessionId"></param>
        /// <returns></returns>
        public static async Task<String> getAppComponent(String ComponentName, string sessionId)
        {
            return await getApplicationComponent(ComponentName, sessionId);
        }


        /// <summary>
        /// This method returns an object representation of a defined component after verifying current user session. This method should be invoked through an ajax call 
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        public static async Task<GenericResponse> getComponent(QuickProcess.Model.getComponent_Model request, string url)
        {
            var Application = await getApplication();

            var Session = await getUserSession(Application, request.SessionId, request.ComponentName.ToString());
            if (Session == null || Session.Item1.ResponseCode != "00")
                return Session.Item1;

            return await fetchComponent(Application, request.ComponentName.ToString().ToLower());
        }


        /// <summary>
        /// This method returns list of records that is displayed by a table/card component
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        public static async Task<SearchResponse> searchRecord(QuickProcess.Model.searchRecord_Model request, string url)
        {
            var Application = await getApplication();

            string PageIndex = request.PageIndex.ToString();
            string PageSize = request.PageSize.ToString();
            string SearchText = (request.SearchText == null) ? "" : request.SearchText.ToString();
            string Download = (request.Download == null) ? "" : request.Download.ToString();
            string RecordID = (request.RecordID == null) ? "" : request.RecordID.ToString();
            string DataSourceParams = (request.DataSourceParams == null) ? "" : request.DataSourceParams.ToString();
            string DownloadFormat = (request.DownloadFormat == null) ? "" : request.DownloadFormat.ToString();
            string AdvanceSearchOptions = (request.AdvanceSearchOptions == null) ? "" : request.AdvanceSearchOptions.ToString();
            string SortOrder = (request.SortOrder == null) ? "" : request.SortOrder.ToString();

            var Session = await getUserSession(Application, request.SessionId.ToString(), request.ComponentName.ToString());
            if (Session == null || Session.Item1.ResponseCode != "00")
                return new SearchResponse() { Response = Session.Item1 };

            return await QuickProcess.Service.searchrecord(Application, url, request, request.ComponentName.ToString(), Session.Item2, SearchText, PageIndex, PageSize, Download, RecordID, DataSourceParams, DownloadFormat, SortOrder);
        }


        /// <summary>
        /// This method returns single record that is displayed by a form component.
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        public static async Task<GenericResponse> getRecord(dynamic request, string url)
        {
            var Application = await getApplication();

            var Session = await getUserSession(Application, request.SessionId.ToString(), request.ComponentName.ToString());
            if (Session == null || Session.Item1.ResponseCode != "00")
                return Session.Item1;

            return await QuickProcess.Service.getRecord(Application, url, request, request.ComponentName.ToString(), Session.Item2, request.Dform.ToString(), request.RecordID.ToString(), (request.DataSourceParams != null) ? request.DataSourceParams.ToString() : null);
        }


        /// <summary>
        /// This method is used to save record from a form component to an underlying datasource
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        public static async Task<GenericResponse> saveRecord(dynamic request, string url)
        {
            string parameters = (request.parameters != null) ? request.parameters.ToString() : "";
            var Application = await getApplication();

            var Session = await getUserSession(Application, request.SessionId.ToString(), request.ComponentName.ToString());
            if (Session == null || Session.Item1.ResponseCode != "00")
                return Session.Item1;

            return await QuickProcess.Service.saveRecord(Application, url, request, request.ComponentName.ToString(), Session.Item2, request.Dform.ToString(), parameters);
        }


        /// <summary>
        /// This method deletes a single record in an underlying datasource for a table/card component
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        public static async Task<GenericResponse> deleteRecord(QuickProcess.Model.deleteRecord_Model request, string url)
        {
            var Application = await getApplication();

            var Session = await getUserSession(Application, request.SessionId.ToString(), request.ComponentName.ToString());
            if (Session == null || Session.Item1.ResponseCode != "00")
                return Session.Item1;

            return await QuickProcess.Service.deleteRecord(Application, url, request, request.ComponentName.ToString(), Session.Item2, request.RecordId.ToString());
        }


        /// <summary>
        /// This method is used to execute sql statement and returns result to calling application. This method is used to encapsulate SQL statement that becomes invokable like an web api
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        public static async Task<GenericResponse> QueryApi(QuickProcess.Model.api_Model request, string url)
        {
            string parameters = (request.parameters != null) ? request.parameters.ToString() : "";
            var Application = await getApplication();

            var Session = await getUserSession(Application, request.SessionId, request.ComponentName.ToString());
            if (Session == null || Session.Item1.ResponseCode != "00")
                return Session.Item1;

            if (request.ComponentName.ToLower() == "getapplicationmodules")
            {
                return await getApplicationModules();
            }

            return await QuickProcess.Service.QueryApi(Application, request.ComponentName.ToString(), Session.Item2, request.Dform.ToString(), parameters);
        }


        /// <summary>
        /// This method is used to invoke custom defined webapi and returns result to calling application. This method is used to encapsulate custom controller action method
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        public static async Task<GenericResponse> WebApi(QuickProcess.Model.api_Model request, string url)
        {
            string parameters = (request.parameters != null) ? request.parameters.ToString() : "";
            var Application = await getApplication();

            var Session = await getUserSession(Application, request.SessionId, request.ComponentName.ToString());
            if (Session == null || Session.Item1.ResponseCode != "00")
                return Session.Item1;

            if (request.ComponentName.ToLower() == "getapplicationmodules")
            {
                return await getApplicationModules();
            }

            return await QuickProcess.Service.WebApi(Application, url, request, request.ComponentName.ToString(), Session.Item2, request.Dform.ToString(), parameters);
        }




        /// <summary>
        /// This method returns a list of records from an underlying datasource, that can be bound to a list input liek select, checklist and autocomplete
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        public static async Task<GenericResponse> getDropDownList(QuickProcess.Model.getDropDownList_Model request, string url)
        {
            string SearchText = "";
            string SearchValue = "";
            string parameters = "";
            try
            {
                SearchText = request.SearchText.ToString();
            }
            catch { }

            try
            {
                SearchValue = request.SearchValue.ToString();
            }
            catch { }

            try
            {
                if (request.parameters != null)
                    parameters = request.parameters.ToString();
            }
            catch { }

            var Application = await getApplication();

            var Session = await getUserSession(Application, request.SessionId.ToString(), request.ComponentName.ToString());
            if (Session == null || Session.Item1.ResponseCode != "00")
                return Session.Item1;

            if (request.ComponentName.ToLower() == "getapplicationmodules")
            {
                return await getApplicationModules();
            }

            return await QuickProcess.Service.getDropDownList(Application, url, request, Session.Item2, SearchText, SearchValue, parameters);
        }


        /// <summary>
        /// This method returns the list of defined modules in the appsettings.json file
        /// </summary>
        /// <returns></returns>
        public static async Task<GenericResponse> getApplicationModules()
        {
            var application = await getApplication();
            List<KeyValue> list = new List<KeyValue>();
            foreach (string resource in application.AuthorisationDetails.Resources)
            {
                list.Add(new KeyValue() { Key = resource, Value = resource });
            }
            return new QuickProcess.GenericResponse() { ResponseCode = "00", Result = Newtonsoft.Json.JsonConvert.SerializeObject(list) };
        }


        /// <summary>
        /// This method returns object of the appsettings file with a list of the components current session is privileged to access
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        public static async Task<String> getApplicationSettings(QuickProcess.Model.api_Model request)
        {
            var Application = await getApplication();
            string UserName = "";
            string app = "";

            if (Application.EnableAuthentication == true)
            {
                try
                {
                    UserName = await validateUserSession(Application, request.SessionId.ToString());
                    if (UserName == null)
                    {
                        dynamic appInfo = new { ThemeColor = Application.ThemeColor, ApplicationTitle = Application.ApplicationTitle, ApplicationName = Application.ApplicationName, Url = Application.Url, FontColor = Application.FontColor, QuickProcessCss_Url = Application.QuickProcessCss_Url, QuickProcessJs_Url = Application.QuickProcessJs_Url, QuickProcessLoadergif_Url = Application.QuickProcessLoadergif_Url };
                        return Newtonsoft.Json.JsonConvert.SerializeObject(new QuickProcess.GenericResponse() { ResponseCode = "00", ResponseDescription = "", Result = JsonSerializer.Serialize(appInfo) });
                    }
                }
                catch
                {
                    dynamic appInfo = new { ThemeColor = Application.ThemeColor, ApplicationTitle = Application.ApplicationTitle, ApplicationName = Application.ApplicationName, Url = Application.Url, FontColor = Application.FontColor, QuickProcessCss_Url = Application.QuickProcessCss_Url, QuickProcessJs_Url = Application.QuickProcessJs_Url, QuickProcessLoadergif_Url = Application.QuickProcessLoadergif_Url };
                    return Newtonsoft.Json.JsonConvert.SerializeObject(new QuickProcess.GenericResponse() { ResponseCode = "00", ResponseDescription = "", Result = JsonSerializer.Serialize(appInfo) });

                }
            }

            if (Application.EnableAuthorisation == true)
            {
                if (string.IsNullOrEmpty(Application.AuthorisationDetails.AuthorisationComponent))
                    return Newtonsoft.Json.JsonConvert.SerializeObject(new QuickProcess.GenericResponse() { ResponseCode = ErrorCode.BadConfiguration, ResponseDescription = "Authorisation Component not defined!" });

                var ResourceGroup = await getUserResourceGroup(Application, UserName);
                List<ComponentList> ComponentList = new List<ComponentList>();

                //iterate through resource group / application modules
                foreach (System.Data.DataRow reourceGroup in ResourceGroup.Rows)
                {
                    //iterate through application componenets to get all components for resource group / module
                    foreach (var componentList in Application.ComponentList)
                    {
                        string resourceGroupName = reourceGroup[0].ToString();
                        var components = new List<ComponentList>();

                        components = Application.ComponentList.Where(cmp => cmp.ResourceGroups != null && cmp.ResourceGroups.Contains(resourceGroupName)).ToList();

                        //check if  component is a member of current resource group and not yet added to list, add component to list
                        foreach (var component in components)
                        {
                            if (ComponentList.Where(exist => exist.Name == component.Name).ToList().Count == 0)
                                ComponentList.Add(new QuickProcess.ComponentList() { Name = component.Name, PreFetchComponent = component.PreFetchComponent });
                        }
                    }
                }

                //override Application.ComponentList object to be serialized and sent to browser
                Application.ComponentList = ComponentList;
            }

            app = JsonSerializer.Serialize(Application);
            return Newtonsoft.Json.JsonConvert.SerializeObject(new QuickProcess.GenericResponse() { ResponseCode = "00", Result = app });
        }


        /// <summary>
        /// Similar to the getApplicationSettings method, this method returns an object representing the appsettings file regardless of current user session
        /// </summary>
        /// <returns></returns>
        public static async Task<String> getApplicationSettings()
        {
            var Application = await getApplication();
            var app = JsonSerializer.Serialize(Application);
            return Newtonsoft.Json.JsonConvert.SerializeObject(new QuickProcess.GenericResponse() { ResponseCode = "00", Result = app });
        }


        /// <summary>
        /// This method is used to refresh QuickProcess Cache where settings of the application is stored in memory
        /// </summary>
        public static async void refreshFramework()
        {
            //if ((await getApplication()).Debug)
            //{
            ObjectCache cache = MemoryCache.Default;
            Application app = Newtonsoft.Json.JsonConvert.DeserializeObject<Application>(await readFile(containers.Properties, containerFiles.application));
            cache.Set("Framework", app, DateTime.Now.AddDays(1));
            //}
        }


        /// <summary>
        /// This method executes an SQL statement asynchronously and returns result as DataTable
        /// </summary>
        /// <param name="SessionId"></param>
        /// <param name="ConnectionName"></param>
        /// <param name="Query"></param>
        /// <param name="Parameters"></param>
        /// <returns>DataTable</returns>
        public static async Task QueryAsync(string? SessionId, string ConnectionName, string Query, dynamic Parameters)
        {
            var Application = await getApplication();
            string ConnectionString = Application.Connections.Where(con => con.ConnectionName.ToLower() == ConnectionName.ToLower()).FirstOrDefault().ConnectionString;
            var session = new UserSession();

            List<SqlParameter> SqlParameters = new List<SqlParameter>();

            //convert parameters to sqlparameters
            if (Parameters != null)
            {
                var ObjParams = Newtonsoft.Json.Linq.JToken.Parse(Newtonsoft.Json.JsonConvert.SerializeObject(Parameters));
                Dictionary<string, string> dictObj = ObjParams.ToObject<Dictionary<string, string>>();

                foreach (var p in dictObj.Keys.ToArray())
                {
                    if (dictObj[p] != null)
                    {
                        string val = dictObj[p].ToString();

                        if (Query.ToLower().Contains("qpencrypt(@" + p.ToLower() + ")"))
                        {
                            val = QuickProcess.Service.EncryptText(val);
                            Query = QuickProcess.Service.ReplaceText(Query, "qpencrypt(@" + p.ToLower() + ")", "@" + p);
                        }

                        if (Query.ToLower().Contains("qphash(@" + p.ToLower() + ")"))
                        {
                            val = QuickProcess.Service.HASH256(val);
                            Query = QuickProcess.Service.ReplaceText(Query, "qphash(@" + p.ToLower() + ")", "@" + p);
                        }

                        SqlParameters.Add(new SqlParameter("@" + p, val));
                    }
                    else
                        SqlParameters.Add(new SqlParameter("@" + p, DBNull.Value));
                }
            }

            //add session and username to parameters
            if (!string.IsNullOrEmpty(SessionId))
            {
                string userName = await validateUserSession(Application, SessionId);
                bool sessionIdExist = false;
                bool userNameExist = false;
                foreach (var param in SqlParameters)
                {
                    if (param.ParameterName.ToLower() == "@const_sessionid")
                        sessionIdExist = true;

                    if (param.ParameterName.ToLower() == "@const_username")
                        userNameExist = true;
                }

                if (!sessionIdExist)
                    SqlParameters.Add(new SqlParameter("@Const_SessionID", SessionId));


                if (!userNameExist)
                    SqlParameters.Add(new SqlParameter("@Const_UserName", userName));
            }


            await QuickProcess.Service.QueryAsync(Application, ConnectionString, Query, SqlParameters);
        }


        /// <summary>
        /// This method executes an SQL statement asynchronously and returns result as generic Object
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="SessionId"></param>
        /// <param name="ConnectionName"></param>
        /// <param name="Query"></param>
        /// <param name="Parameters"></param>
        /// <returns>Generic Object</returns>
        /// <exception cref="Exception"></exception>
        public static async Task<IEnumerable<T>> QueryAsync<T>(string? SessionId, string ConnectionName, string Query, dynamic Parameters)
        {
            var Application = await getApplication();
            var session = new UserSession();
            string ConnectionString = "";
            try
            {
                ConnectionString = Application.Connections.Where(con => con.ConnectionName.ToLower() == ConnectionName.ToLower()).FirstOrDefault().ConnectionString;
            }
            catch { throw new Exception("Invalid connection name"); }


            List<SqlParameter> SqlParameters = new List<SqlParameter>();

            if (Parameters != null)
            {
                //convert parameters to sqlparameters
                var ObjParams = Newtonsoft.Json.Linq.JToken.Parse(Newtonsoft.Json.JsonConvert.SerializeObject(Parameters));
                Dictionary<string, string> dictObj = ObjParams.ToObject<Dictionary<string, string>>();

                foreach (var p in dictObj.Keys.ToArray())
                {
                    if (dictObj[p] != null)
                    {
                        string val = dictObj[p].ToString();

                        if (Query.ToLower().Contains("qpencrypt(@" + p.ToLower() + ")"))
                        {
                            val = QuickProcess.Service.EncryptText(val);
                            Query = QuickProcess.Service.ReplaceText(Query, "qpencrypt(@" + p.ToLower() + ")", "@" + p);
                        }

                        if (Query.ToLower().Contains("qphash(@" + p.ToLower() + ")"))
                        {
                            val = QuickProcess.Service.HASH256(val);
                            Query = QuickProcess.Service.ReplaceText(Query, "qphash(@" + p.ToLower() + ")", "@" + p);
                        }

                        SqlParameters.Add(new SqlParameter("@" + p, val));
                    }
                    else
                        SqlParameters.Add(new SqlParameter("@" + p, DBNull.Value));
                }
            }

            //add session and username to parameters
            if (!string.IsNullOrEmpty(SessionId))
            {
                string userName = await validateUserSession(Application, SessionId);
                bool sessionIdExist = false;
                bool userNameExist = false;
                foreach (var param in SqlParameters)
                {
                    if (param.ParameterName.ToLower() == "@const_sessionid")
                        sessionIdExist = true;

                    if (param.ParameterName.ToLower() == "@const_username")
                        userNameExist = true;
                }

                if (!sessionIdExist)
                    SqlParameters.Add(new SqlParameter("@Const_SessionID", SessionId));


                if (!userNameExist)
                    SqlParameters.Add(new SqlParameter("@Const_UserName", userName));
            }


            return await QuickProcess.Service.QueryAsync<T>(Application, ConnectionString, Query, SqlParameters);
        }


        /// <summary>
        /// This method returns a list of modules defined in the application settings file required for setting up authorisation
        /// </summary>
        /// <returns></returns>
        public static async Task<List<string>> getApplicationResource()
        {
            return (await getApplication()).AuthorisationDetails.Resources;
        }


        /// <summary>
        /// This method returns information of current user's session
        /// </summary>
        /// <param name="SessionId"></param>
        /// <param name="componentName"></param>
        /// <returns></returns>
        public static async Task<Tuple<QuickProcess.GenericResponse, QuickProcess.UserSession>> getSession(string SessionId, string componentName)
        {
            var Application = await getApplication();
            return await getUserSession(Application, SessionId, componentName);
        }



        private static async Task<Tuple<QuickProcess.GenericResponse, QuickProcess.UserSession>> getUserSession(Application Application, string SessionId, string componentName)
        {
            try
            {
                string userName = "";
                string tenantID = "";
                var UserSession = new QuickProcess.UserSession() { SessionID = SessionId, UserName = "", TenantID = "", IsPriviledged = false, IsValid = false };
                var resp = new QuickProcess.GenericResponse() { ResponseCode = "00", ResponseDescription = "Okay" };

                if (Application.EnableAuthentication == false)
                {
                    UserSession = new QuickProcess.UserSession() { SessionID = SessionId, UserName = "Anonymous", TenantID = "", IsPriviledged = true, IsValid = true };
                    return new Tuple<QuickProcess.GenericResponse, QuickProcess.UserSession>(resp, UserSession);
                }

                //todo: validatesession before returning application modules
                if (componentName.ToLower() == "getapplicationmodules")
                    return new Tuple<QuickProcess.GenericResponse, QuickProcess.UserSession>(resp, UserSession);


                //check if component is not session based
                var component = await QuickProcess.Service.getComponent(Application, componentName);
                if (component == null)
                {
                    return null;
                }

                if (!component.SessionBased)
                {
                    UserSession = new QuickProcess.UserSession() { SessionID = SessionId, UserName = "Anonymous", TenantID = "", IsPriviledged = true, IsValid = true };
                    var Status = new QuickProcess.GenericResponse() { ResponseCode = "00", ResponseDescription = "" };
                    return new Tuple<QuickProcess.GenericResponse, QuickProcess.UserSession>(Status, UserSession);
                }


                if (string.IsNullOrEmpty(Application.SessionValidationComponent))
                {
                    UserSession = new QuickProcess.UserSession() { SessionID = SessionId, UserName = userName, TenantID = tenantID, IsValid = true, IsPriviledged = false };
                    resp.ResponseCode = ErrorCode.Wrong_Binding;
                    resp.ResponseDescription = "Session validation component not defined! ";
                }


                //get session validation component
                var sessionValidationComponent = await QuickProcess.Service.getComponent(Application, Application.SessionValidationComponent);

                if (string.IsNullOrEmpty(SessionId))
                {
                    UserSession = new QuickProcess.UserSession() { SessionID = SessionId, UserName = "", TenantID = "", IsValid = false, IsPriviledged = false };
                    resp.ResponseCode = "401";
                    resp.ResponseDescription = "Session Expired";
                    return new Tuple<QuickProcess.GenericResponse, QuickProcess.UserSession>(resp, UserSession);
                }

                if (sessionValidationComponent.Type.ToLower() == "query")
                {
                    var Session = await QuickProcess.Service.ExecQuery(sessionValidationComponent.ConnectionString, sessionValidationComponent.Query, new List<SqlParameter> { new SqlParameter("@Const_SessionID", SessionId), new SqlParameter("@ComponentName", componentName), new SqlParameter("@AppGUID", Application.GUID) });

                    if (Session.Rows.Count == 0)
                    {
                        UserSession = new QuickProcess.UserSession() { SessionID = SessionId, UserName = "", TenantID = "", IsValid = false, IsPriviledged = false };
                        resp.ResponseCode = "401";
                        resp.ResponseDescription = "Session Expired";
                        return new Tuple<QuickProcess.GenericResponse, QuickProcess.UserSession>(resp, UserSession);
                    }
                    userName = Session.Rows[0][0].ToString();

                    if (Session.Columns.Count > 1)
                        tenantID = Session.Rows[0][1].ToString();
                }


                if (await checkAuthorisation(Application, userName, componentName.ToString()) == false)
                {
                    UserSession = new QuickProcess.UserSession() { SessionID = SessionId, UserName = userName, TenantID = tenantID, IsValid = true, IsPriviledged = false };
                    resp.ResponseCode = "403";
                    resp.ResponseDescription = "Access to component '" + componentName.ToString() + "' was denied";
                    return new Tuple<QuickProcess.GenericResponse, QuickProcess.UserSession>(resp, UserSession);
                }


                UserSession = new QuickProcess.UserSession() { SessionID = SessionId, UserName = userName, TenantID = tenantID, IsValid = true, IsPriviledged = true };
                resp = new QuickProcess.GenericResponse() { ResponseCode = "00", ResponseDescription = "Okay", Result = userName };
                return new Tuple<QuickProcess.GenericResponse, QuickProcess.UserSession>(resp, UserSession);

            }
            catch { }

            return null;
        }

        private static async Task<String> validateUserSession(Application Application, string SessionId)
        {
            try
            {
                System.Data.DataTable userSession = new System.Data.DataTable();

                //get session validation component
                var sessionValidationComponent = await QuickProcess.Service.getComponent(Application, Application.SessionValidationComponent);

                if (sessionValidationComponent.Type.ToLower() == "query")
                {
                    userSession = await QuickProcess.Service.ExecQuery(sessionValidationComponent.ConnectionString, sessionValidationComponent.Query, new List<SqlParameter> { new SqlParameter("@Const_SessionID", SessionId) });
                    if (userSession.Rows.Count > 0)
                        return userSession.Rows[0][0].ToString();
                    else
                        return null;
                }
            }
            catch { }

            return null;
        }

        private static async Task<bool> checkAuthorisation(Application app, string UserName, string ComponentName)
        {
            var Component = await QuickProcess.Service.getComponent(app, ComponentName);

            //if authorisation is not enabled then stop validation authorisation access
            if (app.EnableAuthorisation == false)
                return true;

            //if component is not restricted to certain modules then stop validation authorisation access
            if (Component.ResourceGroups == null || Component.ResourceGroups.Count == 0)
                return true;

            //get authorisation component connection name
            var connectionName = (await QuickProcess.Service.getComponent(app, app.AuthorisationDetails.AuthorisationComponent)).Connection.GUID;
            var connection = app.Connections.Where(con => con.ConnectionName.ToLower() == connectionName.ToLower()).FirstOrDefault();

            if (!Component.SessionBased)
                return true;

            if (Component.ResourceGroups == null)
                return false;

            var ResourceGroup = await getUserResourceGroup(app, UserName);
            foreach (System.Data.DataRow row in ResourceGroup.Rows)
            {
                if (Component.ResourceGroups.Where(resource => resource.ToLower() == row[0].ToString().ToLower()).ToList().Count > 0)
                    return true;
            }

            return false;
        }

        private static async Task<System.Data.DataTable> getUserResourceGroup(Application app, string UserName)
        {
            //for now, QuickProcess assumes AuthorisatioComponent can only be a QueryAPI Component
            var authorisationComponent = (await QuickProcess.Service.getComponent(app, app.AuthorisationDetails.AuthorisationComponent));
            var connection = app.Connections.Where(con => con.ConnectionName.ToLower() == authorisationComponent.Connection.GUID.ToLower()).FirstOrDefault();
            return await QuickProcess.Service.ExecQuery(connection.ConnectionString, authorisationComponent.Query, new List<SqlParameter> { new SqlParameter("@Const_UserName", UserName) });
        }

        private static async Task<String> getApplicationComponent(string ComponentName, string SessionId)
        {
            var Application = await getApplication();

            if (Application.EnableAuthentication && string.IsNullOrEmpty(Application.SessionValidationComponent))
            {
                return "Session validation component not defined! ";
            }

            var Session = await getUserSession(Application, SessionId, ComponentName);
            if ((Session == null || Session.Item1.ResponseCode == ErrorCode.SessionExpired) && ComponentName.ToLower() != Application.DefaultComponent.ToLower())
                return "<script>window.location='" + Application.DefaultComponent + ((string.IsNullOrEmpty(ComponentName) == false) ? "?redirectComponent=" + ComponentName : "") + "'</script>";


            string page = "";
            string bootstrap_css = "<link rel=\"stylesheet\" href=\"https://cdnjs.cloudflare.com/ajax/libs/bootstrap/5.1.3/css/bootstrap.min.css\" integrity=\"sha512-GQGU0fMMi238uA+a/bdWJfpUGKUkBdgfFdgBm72SUQ6BeyWjoY/ton0tEjH+OSH9iP4Dfh+7HM0I9f5eR0L/4w==\" crossorigin=\"anonymous\" referrerpolicy=\"no-referrer\" />";
            string bootstrap_js = "<script src=\"https://stackpath.bootstrapcdn.com/bootstrap/4.4.1/js/bootstrap.min.js\" integrity=\"sha384-wfSDF2E50Y2D1uUdj0O3uMBJnjuUD4Ih7YwaYd1iqfktj0Uod8GCExl3Og8ifwB6\" crossorigin=\"anonymous\"></script>";
            string popper_js = "<script src=\"https://cdnjs.cloudflare.com/ajax/libs/popper.js/1.11.0/umd/popper.min.js\" integrity=\"sha384-b/U6ypiBEHpOf/4+1nzFpr53nxSS+GLCkfwBdFNTxtclqqenISfwAzpKaMNFNmj4\" crossorigin=\"anonymous\"></script>";
            string fontawesome = "<link href=\"https://maxcdn.bootstrapcdn.com/font-awesome/4.2.0/css/font-awesome.min.css\" rel=\"stylesheet\">";
            string quickprocess_js = " </script> <script src=\"" + Application.QuickProcessJs_Url + "\"></script>";
            string quickprocess_css = "<link rel=\"stylesheet\" href=\"" + Application.QuickProcessCss_Url + "\" /> ";
            string jquery_js = "<script src=\"https://cdnjs.cloudflare.com/ajax/libs/jquery/3.5.1/jquery.min.js\" integrity=\"sha512-bLT0Qm9VnAYZDflyKcBaQ2gg0hSYNQrJ8RilYldYQ1FxQYoCLtUjuuRuZo+fjqhx/qtq/1itJ0C2ejDxltZVFg==\" crossorigin=\"anonymous\" referrerpolicy=\"no-referrer\"></script>";
            string sweetalert_js = "<script src=\"https://unpkg.com/sweetalert/dist/sweetalert.min.js\"></script>";


            string componentInfo = "";
            try
            {
                var fetch_Result = await fetchComponent(Application, ComponentName.ToString());
                if (fetch_Result.ResponseCode != "00")
                    return fetch_Result.ResponseDescription;
                else
                    componentInfo = fetch_Result.Result;
            }
            catch { return "No default component found!"; }

            var component = JsonSerializer.Deserialize<QuickProcess.Component>(componentInfo);


            // string theme = "<style>.qp-bg-theme{background-color:" + Application.ThemeColor + "; color:" + Application.FontColor + " } .qp-color-theme{color:" + Application.ThemeColor + ";}</style>";

            string initscript = " <script> $(document).ready(function () {  qpStartProcess(getCookie('SessionId'), \"" + Application.GUID + "\",\"" + Application.Debug + "\",\"" + component.Name + "\",\"" + Application.QuickProcessLoadergif_Url + "\"); }); </script>";

            if (component.IsLayoutComponent)
            {
                page = quickprocess_css + component.Markup; //quickprocess_css+ theme+ component.Markup
                page += "\r\n<script id = \"" + component.Name + "_script\">\r\n\r\n$('#" + component.Name + "_script').remove();\r\n\r\nfunction qp_layout_load()\r\n{\r\n" + component.Name + "_Load();\r\n}\r\n" + component.ComponentEvents + " " + component.ComponentFunctions + "  \r\n</script>";
                page += "\r\n" + quickprocess_js + "\r\n" + initscript + sweetalert_js;
            }
            else
            {
                //get component layout page if defined
                if (!string.IsNullOrEmpty(component.LayoutPage))
                {
                    var Layout_Component = JsonSerializer.Deserialize<QuickProcess.Component>((await fetchComponent(Application, component.LayoutPage)).Result);
                    page = Layout_Component.Markup.Replace("@RenderComponent", "<div id=\"root\"><div datasource=\"" + component.Name + "\" type=\"component\"></div></div>");
                    //var css = bootstrap_css + fontawesome + quickprocess_css + theme;
                    var css = quickprocess_css;// + theme;
                    //var scripts = jquery_js + popper_js + bootstrap_js + sweetalert_js + quickprocess_js + initscript;
                    var scripts = quickprocess_js + initscript;
                    scripts += "\r\n<script id = \"" + Layout_Component.Name + "_script\">\r\n\r\n$('#" + Layout_Component.Name + "_script').remove();\r\n\r\nfunction qp_layout_load()\r\n{qp.currentLayout='" + component.LayoutPage + "'; \r\n\r\n\r\n" + Layout_Component.Name + "_Load();\r\n}\r\n" + Layout_Component.ComponentEvents + " " + Layout_Component.ComponentFunctions + "  \r\n</script>";

                    if (page.Contains("@DependencyScriptSection"))
                    {
                        page = css + page.Replace("@DependencyScriptSection", scripts);
                    }
                    else
                    {
                        page = css + page + scripts;
                    }
                }
                else
                {
                    var css = bootstrap_css + fontawesome + quickprocess_css;// + theme;
                    var scripts = jquery_js + popper_js + bootstrap_js + sweetalert_js + quickprocess_js + initscript;
                    page = "<head>" + css + "</head><div id=\"root\"><div datasource=\"" + component.Name + "\" type=\"component\"></div></div>" + scripts + " ";
                }
            }

            return page;
        }

        private static async Task<QuickProcess.GenericResponse> fetchComponent(Application Application, string ComponentName)
        {
            string UrlPath = "../../";
            return await QuickProcess.Service.FetchComponent(Application, ComponentName, UrlPath);
        }

        public static async Task<Application> getApplication()
        {
            ObjectCache cache = MemoryCache.Default;
            var Framework = cache.Get("Framework") as dynamic;

            if (Framework == null)
            {
                Application app = Newtonsoft.Json.JsonConvert.DeserializeObject<Application>(await readFile(containers.Properties, containerFiles.application));
                cache.Set("Framework", app, DateTime.Now.AddDays(1));
            }

            Application Application = cache.Get("Framework") as Application;
            string info = Newtonsoft.Json.JsonConvert.SerializeObject(Application);
            return Newtonsoft.Json.JsonConvert.DeserializeObject<Application>(info);
        }

        private static async Task<string> readFile(string containerName, string fileName)
        {
            var binDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            var rootDirectory = Path.GetFullPath(Path.Combine(binDirectory, ".."));
            string path = "";
            if (Directory.Exists(binDirectory + "\\QuickProcess\\"))
                path = binDirectory + "\\QuickProcess\\";

            //consider azure
            if (Directory.Exists(rootDirectory + "\\QuickProcess\\"))
                path = rootDirectory + "\\QuickProcess\\";

            string file = "";
            if (File.Exists(path + containerName + "\\" + fileName))
                file = System.IO.File.ReadAllText(path + containerName + "\\" + fileName);
            return file;
        }

        private static async Task writeFile(string containerName, string fileName, string content)
        {
            var binDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            var rootDirectory = Path.GetFullPath(Path.Combine(binDirectory, ".."));
            string path = "";
            if (Directory.Exists(binDirectory))
                path = binDirectory;

            if (Directory.Exists(rootDirectory))
                path = rootDirectory;

            System.IO.File.WriteAllText(path + containerName + "\\" + fileName, content);
        }

        private static class containers
        {
            public static string Properties = "Properties";
        }

        private static class containerFiles
        {
            public static string application = "application.json";
        }
    }
}
