using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Text;

namespace QuickProcess
{
    public class Model
    {
        public  class apiSearchResponseModel
        {
            public List<dynamic>? Records { get; set; }
            public  int? TotalRecordCount { get; set; }
        }
            
        public class DbAttribute : Attribute
        {
            public bool IsIdentity { get; set; }
            public bool KeyColumn { get; set; }
        }

        public class getComponent_Model : QuickProcess_SaveData_Baseclass
        {

        }

        public class searchRecord_Model : QuickProcess_SaveData_Baseclass
        {
            public string PageIndex { get; set; }
            public string PageSize { get; set; }
            public string? SearchText { get; set; }
            public string SortOrder { get; set; }
            public string? Download { get; set; }
            public string? RecordID { get; set; }
            public string? DataSourceParams { get; set; }
            public string? DownloadFormat { get; set; }
            public string? AdvanceSearchOptions { get; set; }

            public SearchResponse setData<T>(List<T> objects, [System.Runtime.CompilerServices.CallerMemberName] string callerName = "")
            {
                //StackFrame frame = new StackFrame(1);
                //var s1 = frame.GetMethod().Name;
                //var s2 = frame.GetMethod().DeclaringType.Name;

                string[,] data =new string[0,0];
                PagerSetup pagerSetup = new PagerSetup();
                return new SearchResponse() { Data= data, Response=new GenericResponse(), PagerSetup=pagerSetup };
            }
        }

        public class saveRecord_Model : QuickProcess_SaveData_Baseclass
        {
            public string? SaveStatus { get; set; }

            /// <summary>
            /// saves posted parameters to component datasource table using underlying connection of the associated component
            /// and returns generic response of the operation status the the colling point
            /// </summary>
            /// <returns>returns generic response of the save operation status</returns>
            public async Task<GenericResponse> saveRecord()
            {
                return await QuickProcess_Core.saveRecord(this,"");
            }

            /// <summary>
            /// saves posted parameters into the component datasource table using he underlying connection of the associated component
            /// and returns an object structure matching the parameter object type
            /// </summary>
            /// <typeparam name="T"></typeparam>
            /// <returns>returns an object of the newly created/updated record</returns>
            public async Task<T> saveRecord<T>()
            {
                Response = await QuickProcess_Core.saveRecord(this,"");
                SaveStatus = Response.ResponseCode;
                return Newtonsoft.Json.JsonConvert.DeserializeObject<T>(Response.Result);
            }
        }

        public class fetchRecord_Model : QuickProcess_SaveData_Baseclass
        {
            public string RecordID { get; set; }
            public string? DataSourceParams { get; set; }
            public string? FetchStatus { get; set; }

            /// <summary>
            /// fetches a single record from the database using the underlying connection of the associated component
            /// </summary>
            /// <returns></returns>
            public async Task<QuickProcess.GenericResponse> fetchRecord()
            {
                Response = await QuickProcess_Core.getRecord(this,"");
                FetchStatus = Response.ResponseCode;
                return Response;
            }

            /// <summary>
            /// fetches a single record from the database using the underlying connection of the associated component
            /// </summary>
            /// <returns></returns>
            public async Task<T> fetchRecord<T>()
            {
                this.Response = await QuickProcess_Core.getRecord(this,"");
                FetchStatus = Response.ResponseCode;
                return Newtonsoft.Json.JsonConvert.DeserializeObject<T>(this.Response.Result);
            }
        }
        
        public class api_Model : QuickProcess_SaveData_Baseclass
        {
            public string? SearchText { get; set; }
            public string? SearchValue { get; set; }
            /// <summary>
            /// Saves multiple objects to database tables using the underlying connection of the associated component
            /// </summary>
            /// <typeparam name="T"></typeparam>
            /// <param name="objects">Object to be saved to database</param>
            /// <param name="AddOrUpdate"></param>
            /// <param name="callerName"></param>
            /// <returns></returns>
            public override async Task<List<T>> saveObjects<T>(List<T> objects, bool? AddOrUpdate = true, string? connectionString = "", [System.Runtime.CompilerServices.CallerMemberName] string callerName = "")
            { 
                return await base.saveObjects<T>(objects, AddOrUpdate, connectionString, callerName);
            }
        }

        public class deleteRecord_Model : QuickProcess_SaveData_Baseclass
        {
            public string RecordId { get; set; }
        }

        public class getDropDownList_Model : QuickProcess_SaveData_Baseclass
        {
            public string? SearchText { get; set; }
            public string? SearchValue { get; set; }
        }
    }


    public class UserSession
    {
        public string SessionID { get; set; }
        public string UserName { get; set; }
        public string TenantID { get; set; }
        public bool IsValid { get; set; }
        public bool IsPriviledged { get; set; }
    }


    public class SearchResponse
    {
        public PagerSetup PagerSetup { get; set; }
        public string[,] Data { get; set; }
        public GenericResponse Response { get; set; }
    }

    public class PagerSetup
    {
        public bool FirstPage { get; set; }
        public bool LastPage { get; set; }
        public bool ReducePageIndex { get; set; }
        public bool IncreasePageIndex { get; set; }
        public string[] InViewPageIndices { get; set; }
        public int ActiveIndex { get; set; }
        public bool NextPageList { get; set; }
        public bool PrevPageList { get; set; }
        public int TotalRecord { get; set; }
        public int TotalPages { get; set; }
        public string Columns { get; set; }
        public string UColumn { get; set; }
    }

    public class AuthorisationDetails
    {
       public string AuthorisationComponent { get; set; }
        public List<string> Resources { get; set; }
    }

    


    public class ConnectionList
    {
        public string GUID { get; set; }
        public string ConnectionName { get; set; }
        public string Engine { get; set; }
        public string ConnectionString { get; set; }
        public bool isDefaultConnection { get; set; }
    }

    public class ExternalDomainList
    {
        public string GUID { get; set; }
        public string DomainName { get; set; }
        public string Url { get; set; }
    }

    public class Application
    {
        public string GUID { get; set; }
        public string ThemeColor { get; set; }
        public string FontColor { get; set; }
        public string Dev_Path { get; set; }
        public string Output_Path { get; set; }
        public string Url { get; set; }
        public string Status { get; set; }
        public string ApplicationTitle { get; set; }
        public string DefaultComponent { get; set; }             
        public string DefaultConnection { get; set; }
        public string DefaultDomain { get; set; }
        public string ApplicationName { get; set; }
        public string PreFetchComponents { get; set; }
        public string SessionValidationComponent { get; set; }
        public string QuickProcessJs_Url { get; set; }
        public string QuickProcessCss_Url { get; set; }
        public string QuickProcessLoadergif_Url { get; set; }
        public string Default_FormWrapperMarkup { get; set; }
        public string Default_ListWrapperMarkup { get; set; }
        public bool Debug { get; set; }
        public bool EnableAuthentication { get; set; }
        public bool EnableAuthorisation { get; set; }
        public AuthorisationDetails AuthorisationDetails { get; set; }
        public List<ConnectionList> Connections { get; set; }
        public List<ExternalDomainList> ExternalDomains { get; set; }
        public List<ComponentList> ComponentList { get; set; }
        public string PublishUrl { get; internal set; }
        public string PublishKey { get; internal set; }

        public ConnectionList getConnection(string connetionGUID)
        {
            return this.Connections.Where(con => con.GUID.ToLower() == connetionGUID.ToLower()).FirstOrDefault();
        }
    }

    public class GenericResponse
    {
        public string ResponseCode { get; set; }
        public string ResponseDescription { get; set; }
        public string Result { get; set; }
    }

 
    internal class FormControls
    {
        public string FieldName { get; set; }
        public string Title { get; set; }
        public string Value { get; set; }
        public string ControlType { get; set; }
        public string DataForm { get; set; }// indicates if data should be hashed or encrypted in db
        public bool Compulsory { get; set; }
        public string DataSource { get; set; }
        public ControlList[] List { get; set; }
        public bool EnableInsert { get; set; } = true;
        public bool EnableSave { get; set; } = true; //works for both insert and update
        public bool EnableUpdate { get; set; } = true;
        public bool EnableFetch { get; set; } = true;
        public bool ReadOnly { get; set; }
        public string DefaultValue { get; set; }
        public string DataType { get; set; } = "string";
        public int MaxLength { get; set; }
    }

    internal class ControlList
    {
        public string Key { get; set; }
        public string Value { get; set; }
    }

    internal static class ControlType
    {
        public static string Text = "text";
        public static string TextArea = "textarea";
        public static string LABEL = "label";
        public static string CheckBox = "checkbox";
        public static string ColourPicker = "color";
        public static string Date = "date";
        public static string Month = "month";
        public static string Week = "week";
        public static string EMAIL = "email";
        public static string Search = "search";
        public static string Number = "number";
        public static string Phone = "tel";
        public static string HIDDEN_FIELD = "hidden";
        public static string Range = "range";
        public static string PASSWORD = "password";
        public static string Text_Area = "text_area";
        public static string Static_DropDown = "static_dropdown";
        public static string Dynamic_DropDown = "dynamic_dropdown";
        public static string DateTime = "datetime";
        public static string Toggle = "toggle";
        public static string CheckList = "static_checklist";
        public static string Dynamic_CheckList = "dynamic_checklist";


        public static string ServerField = "serverfield";
        public static string CONST_USERNAME = "@const_username";
        public static string CONST_SESSIONID = "@const_sessionid";
        public static string CONST_TENANTID = "@const_tenantid";
        public static string CurrentDateTime = "current_datetime";
        public static string GUID = "guid";
        public static string RandomNumber = "randomnumber";
        public static string IPAddress = "ipaddress";
        public static string Geolocation = "geolocation";


        public static string User_Phone_Validator = "user_phone_validator";
        public static string User_Email_Validator = "user_login_validator";
        public static string User_PIN_Validator = "user_pin_validator";
        public static string User_Password_Validator = "user_password_validator";


        public static string TABLE_LOOKUP = "tablelookup";
        public static string AutoComplete = "autocomplete";
        public static string ToggleList = "togglelist";
        public static string RadioButtonList = "radiolist";
        public static string Rating = "rating";
        public static string SignPad = "signpad";
        public static string BadgeListGroup = "badgelist";
        public static string FILEUPLOAD = "file";  
        

        public static string HtmlEditor = "texteditor";
        public static string Number_CommaSeparated = "number_cs";
        public static string Money = "money";
        public static string Static_CheckList = "static_checklist";
        public static string Dynamic_ImageLookup = "DYNAMIC IMAGE LOOKUP";
        public static string Static_ImageLookup = "STATIC IMAGE LOOKUP";
        public static string StaffLookUp = "STAFFLOOKUP";         
        public static string ImageUpload = "IMAGE UPLOAD";

    }

    internal class KeyValue
    {
        public string Key { get; set; }
        public string Value { get; set; }
    }

    internal class UniqueColumn
    {
        public string ColumnName { get; set; }
        public string DataType { get; set; }
        public bool isIdentity { get; set; }
    }

  
    internal class Columns
    {
        public string ColumnName { get; set; }
        public string HeaderText { get; set; }
        public bool Searchable { get; set; }
        public string DataTypeFormat { get; set; }
        public bool WrapText { get; set; }
        public bool Visible { get; set; }
        public string Sort { get; set; } = "none";
        public string Align { get; set; } = "left";
        public string Width { get; set; }
        public string Height { get; set; }
        public bool IsBound { get; set; } = true;
        public string DataType { get; set; }
        public string Template { get; set; }
    }

    internal class ComponentConnection
    {
        public string Engine { get; set; }
        public string GUID { get; set; }
    }

    internal class SortOrder
    {
        public int ColumnIndex { get; set; }
        public string Order { get; set; }
    }

    internal static class DataSourceType
    {
        public static string Table = "table";
        public static string View = "view";
        public static string Function = "function";
        public static string Procedure = "procedure";
        public static string Query = "query";
        public static string API = "api";
    }

    public static class ComponentType
    {
        public static string HTML = "html";
        public static string TableList = "table";
        public static string CardList = "card";
        public static string Form = "form";
        public static string Query = "query";
        public static string DropDownList = "dropdown";
        public static string API = "api";
    }

    internal class Component
    {
        public string FileName { get; set; }
        public string Type { get; set; }
        public string Name { get; set; }
        public bool SessionBased { get; set; }
        public List<string> ResourceGroups { get; set; }
        public string About { get; set; }
        public bool IsLayoutComponent { get; set; }
        public bool PreFetchComponent { get; set; }
        public bool DefaultComponent { get; set; }
        public bool AutoLoad { get; set; }
        public string DataSourceType { get; set; }
        public string DataSourceTable { get; set; }
        public string Schema { get; set; }
        public string Icon { get; set; }
        public string Title { get; set; }
        public string CurrentQueryString { get; set; }
        public ComponentConnection Connection { get; set; }
        public int ConnectionTimeout { get; set; } = 30;
        public string ConnectionString { get; set; }
        public bool Disabled { get; set; }
        public string WrapperMarkup { get; set; } = "@RenderComponent";
        public string ComponentEvents { get; set; }
        public string ComponentFunctions { get; set; }
        public string ThemeColor { get; set; }
        public string FetchMethod { get; set; }
        public string PostMethod { get; set; }
        public string FetchUrl { get; set; }
        public string PostUrl { get; set; }
        public string Route { get; set; }
        public string FolderPath { get; set; }

        //-----------Designer Fields--------------//
        public string Designer_QueryColumns { get; set; }
        public string Designer_QueryParams { get; set; }


        //-----------List Features--------------//
        public Columns[] Columns { get; set; }
        public string CustomSelectQuery { get; set; }
        public string CustomDeleteQuery { get; set; }
        public string RecordTemplate { get; set; }
        public string RecordMenuTemplate { get; set; }
        public string loadDataMode { get; set; }
        public string onRecordClick { get; set; }
        public bool EnableSorting { get; set; }
        public bool EnablePagination { get; set; }
        public bool EnableSearch { get; set; }
        public bool EnableExport { get; set; }
        public int DefaultPageSize { get; set; }
        public bool ShowRecordMenu { get; set; }
        public bool ShowStripes { get; set; }
        public bool ShowHoverEffect { get; set; }
        public bool ModalForm { get; set; } = false;
        public string PaginationPosition { get; set; }
        public string NewRecordText { get; set; } = "New Record";
        public bool ShowIcon { get; set; }
        public bool ShowTitle { get; set; }
        public bool ModalProgress { get; set; }
        public bool ShowRecordCount { get; set; }
        public bool AutoSearch { get; set; }
        public bool ShowRefresh { get; set; } = true;
        public string EmptyRecordTemplate { get; set; }
        public string FormComponent { get; set; }

        //----------------List and Form Features--------------------//
        public UniqueColumn UniqueColumn { get; set; }
        public string FormUniqueColumn { get; set; }
        public string QueryParameters { get; set; }
        public bool EnableInsert { get; set; } = false;
        public bool EnableEdit { get; set; } = false;
        public bool EnableDelete { get; set; } = false;
        public bool EnableView { get; set; } = false;
        public bool ShowBorder { get; set; }
        public string QueryStringFilter { get; set; }


        //----------------Form Features--------------------//
        public int GridDisplay { get; set; }
        public FormControls[] Controls { get; set; }
        public string CustomInsertQuery { get; set; }
        public string CustomUpdateQuery { get; set; }


        //---------------Dropdown List Features----------// 
        public string DisplayField { get; set; }
        public string ValueField { get; set; }


        //---------------API Query Features----------//
        public string Query { get; set; }
        public string ApiUrl { get; set; }
        //public EndPoint EndPoint { get; set; }


        //---------------HTML Features----------//
        public string Markup { get; set; }
        public string LayoutPage { get; set; }
        public string src { get;  set; }
    }

   

    public class ComponentList
    {
        public string Name { get; set; }
        public bool PreFetchComponent { get; set; }
        public List<string> ResourceGroups { get; set; }
        public string FolderPath { get; set; }
    }

    

    internal static class ErrorCode
    {
        public static string Component_Not_Found = "404";
        public static string Wrong_Binding = "99";
        public static string WrongEndpointCall = "403";
        public static string Forbidden= "405";
        public static string SessionExpired = "401";
        public static string BadConfiguration = "501";
    }

    //internal class EndPoint
    //{
    //    public string Method { get; set; }
    //    public string DomainName { get; set; }
    //    public string RelativePath { get; set; }

    //}



}
