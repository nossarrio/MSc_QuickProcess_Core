using System;
using System.Collections;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Data;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using System.Diagnostics;
using Microsoft.Extensions.Logging;
using System.Text.RegularExpressions;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Newtonsoft.Json;
using System.Dynamic;
using System.Net;
using System.Net.Http.Headers;

namespace QuickProcess
{
    internal static class Service
    {
        internal static async Task<SearchResponse> searchrecord(Application Application, string url, QuickProcess.Model.searchRecord_Model request, string ComponentName, UserSession UserSession, string SearchText, string PageIndex, string PageSize, string Download, string RecordID, string ParameterFilter, string DownloadFormat, string SortOrder)
        {
            System.Threading.Thread.CurrentThread.CurrentCulture = new System.Globalization.CultureInfo("en-GB");
            DataTable records = new DataTable();
            int TotalRecordCount = 0;
            SearchResponse resp = new SearchResponse();
            resp.Response = new GenericResponse();

            var Component = await getComponent(Application, ComponentName);

            if (Component == null)
            {
                resp.Response.ResponseCode = ErrorCode.Component_Not_Found;
                resp.Response.ResponseDescription = "Component [" + ComponentName + "] not found";
                return resp;
            }

            if (Component.Type.ToLower() != "table" && Component.Type.ToLower() != "card")
            {
                resp.Response.ResponseCode = ErrorCode.WrongEndpointCall;
                resp.Response.ResponseDescription = "Wrong method endpoint call for Component: [" + ComponentName + "]";
                return resp;
            }

            if (Component.FetchMethod == "api" && string.IsNullOrEmpty(url) == false)
            {
                var response = JsonConvert.DeserializeObject<GenericResponse>(await postUrl(url, Component.FetchUrl, request));
                var result = JsonConvert.DeserializeObject<Model.apiSearchResponseModel>(response.Result);
                dynamic result_records = JsonConvert.DeserializeObject<List<Dictionary<string, string>>>(JsonConvert.SerializeObject(result.Records));
                records = generateRecordTable(Component, result_records, PageIndex, PageSize, result.TotalRecordCount ?? 0);
                TotalRecordCount = result.TotalRecordCount == null ? 0 : int.Parse(result.TotalRecordCount.ToString());
            }
            else
            {
                SqlParameter[] CapturedByParams = new SqlParameter[0];
                SqlParameter[] PendingParams = new SqlParameter[0];
                ArrayList SqlParameters = new ArrayList();


                //add columns to select query
                string query = "";
                string columnsToSelect = "";
                string whereClause = "";

                if (Component.Columns != null)
                {
                    foreach (var column in Component.Columns)
                    {
                        if (column.Visible == false && Download.ToLower() == "true")
                        {
                            //do nothing.. this simply prevents export of hidden columns
                        }
                        else
                        {
                            if (column.IsBound)
                                columnsToSelect += "[" + column.ColumnName + "],";
                        }

                    }
                    if (columnsToSelect != "")
                        columnsToSelect = columnsToSelect.Substring(0, columnsToSelect.Length - 1);


                    //generate where clause for searchable columns

                    foreach (var col in Component.Columns)
                    {
                        if (col.Searchable == true && col.IsBound)
                        {
                            whereClause += " isnull([" + col.ColumnName + "],'') +";
                        }
                    }

                }

                if (whereClause != "")
                {
                    //add where clause to query
                    whereClause = whereClause.Substring(0, whereClause.Length - 1);
                    query += " where " + whereClause + " like  Concat('%',@Const_SearchText,'%') ";
                    SqlParameters.Add(new SqlParameter("@Const_SearchText", SearchText));
                }


                //filter by parameter if any             
                if (string.IsNullOrEmpty(ParameterFilter) == false)
                {
                    var filter = Newtonsoft.Json.JsonConvert.DeserializeObject<KeyValue[]>(ParameterFilter);
                    ArrayList parametersAdded = new ArrayList();
                    foreach (var key_val in filter)
                    {
                        if (Component.Columns != null)
                        {
                            foreach (var col in Component.Columns)
                            {
                                if (col.ColumnName.ToLower() == key_val.Key.ToLower() && col.IsBound)
                                {
                                    //alter query only IF datasource type is table or view otherwise only paramters should be sent to db
                                    if (Component.DataSourceType == DataSourceType.Table || Component.DataSourceType == DataSourceType.View)
                                    {
                                        if (query.Contains(" where ") == false)
                                        {
                                            query = " where [" + col.ColumnName + "]=@" + col.ColumnName.Replace(" ", "");
                                        }
                                        else
                                        {
                                            query += " and [" + col.ColumnName + "]=@" + col.ColumnName.Replace(" ", "");
                                        }
                                    }

                                    parametersAdded.Add(key_val.Key.ToLower());
                                    SqlParameters.Add(new SqlParameter("@" + col.ColumnName.Replace(" ", ""), CastDataType(col.DataType, key_val.Value)));
                                    break;
                                }
                            }
                        }
                    }

                    //add other parameters that don't match column name
                    foreach (var key_val in filter)
                    {
                        if (parametersAdded.Contains(key_val.Key.ToLower()) == false)
                            SqlParameters.Add(new SqlParameter("@" + key_val.Key.ToString().Replace(" ", ""), key_val.Value));
                    }
                }


                //filter by RecordID if not empty
                if (RecordID != "")
                {
                    query += " and " + Component.UniqueColumn.ColumnName + "=@" + Component.UniqueColumn.ColumnName + "_";
                }

                //add other parameters 
                SqlParameters.Add(new SqlParameter("@" + Component.UniqueColumn.ColumnName + "_", RecordID));



                int index = 0;
                PendingParams = new SqlParameter[SqlParameters.Count];
                foreach (SqlParameter param in SqlParameters)
                {
                    PendingParams[index] = param;
                    index += 1;
                }


                if (Download.ToLower() == "true")
                {
                    var result = await generateSearchQuery(Component, columnsToSelect, query, UserSession, PendingParams, int.Parse(PageSize), 0, TotalRecordCount, SortOrder, Component.CustomSelectQuery, true);
                    records = result.Item1;
                    TotalRecordCount = result.Item2;
                }
                else
                {
                    var result = await generateSearchQuery(Component, columnsToSelect, query, UserSession, PendingParams, int.Parse(PageSize), int.Parse(PageIndex) * int.Parse(PageSize), int.Parse(PageSize), SortOrder, Component.CustomSelectQuery, false);
                    records = result.Item1;
                    TotalRecordCount = result.Item2;
                }
            }


            //remove undefined columns
            List<string> columnsToRemove = new List<string>();
            foreach (DataColumn column in records.Columns)
            {
                if (Component.Columns.Where(col => col.ColumnName.ToLower() == column.ColumnName.ToLower()).ToList().Count == 0)
                {
                    columnsToRemove.Add(column.ColumnName);
                }
            }
            foreach (string coltoRemove in columnsToRemove)
            {
                records.Columns.Remove(coltoRemove);
            }


            //check if a column defined does not match columns returned in datasource
            if (records.Rows.Count > 0)
            {
                foreach (Columns col in Component.Columns)
                {
                    try
                    {
                        records.Rows[0][col.ColumnName].ToString();
                    }
                    catch
                    {
                        throw new Exception("Column: \"" + col.ColumnName + "\" was not found in the underlying datasource");
                    }
                }
            }

            //restructure table
            int ordinalIndex = 0;
            foreach (Columns col in Component.Columns)
            {
                try
                {
                    records.Columns[col.ColumnName].SetOrdinal(ordinalIndex);
                    ordinalIndex += 1;
                }
                catch { }
            }



            //reset pageindex if necessary
            if (int.Parse(PageIndex) >= records.Rows.Count || int.Parse(PageSize) >= records.Rows.Count)
                PageIndex = "0";

            if (Component.EnablePagination == false)
                PageSize = TotalRecordCount.ToString();

            //transform records to multi-dimensional array to be sent back to browser 
            string[,] responserecords = new string[0, 0];

            try
            {
                responserecords = new string[records.Rows.Count, records.Columns.Count];
            }
            catch
            {
                PageIndex = "0";
                responserecords = new string[int.Parse(PageSize), records.Columns.Count];
            }

            try
            {
                if (Download.ToLower() == "true")
                {
                    responserecords = new string[records.Rows.Count, records.Columns.Count];

                    if (DownloadFormat.ToLower() == "print" || DownloadFormat.ToLower() == "csv")
                    {
                        for (int i = 0; i < records.Rows.Count; ++i)
                        {
                            for (int j = 0; j < records.Columns.Count; ++j)
                            {
                                responserecords[i, j] = records.Rows[i][j].ToString();
                            }
                        }
                        resp.Data = responserecords;
                    }


                    //generate empty pager
                    resp.PagerSetup = (PagerSetup)generatePager(null, PageIndex, PageSize, 0);

                }
                else
                {
                    int startindex = (int.Parse(PageIndex) * int.Parse(PageSize));
                    try
                    {
                        for (int i = startindex; i < (startindex + int.Parse(PageSize)); ++i)
                        {
                            for (int j = 0; j < records.Columns.Count; ++j)
                            {
                                responserecords[i - (int.Parse(PageIndex) * int.Parse(PageSize)), j] = records.Rows[i][j].ToString();
                            }
                        }
                    }
                    catch { }

                    //generate pagerSetup
                    var pager = (PagerSetup)generatePager(records, PageIndex, PageSize, TotalRecordCount);
                    pager.UColumn = Component.UniqueColumn.ColumnName;

                    pager.Columns += "[";
                    int colindex = 0;
                    foreach (DataColumn col in records.Columns)
                    {
                        pager.Columns += "{\"" + colindex + "\":\"" + col.ColumnName + "\"},";
                        colindex += 1;
                    }
                    pager.Columns = pager.Columns.Substring(0, pager.Columns.Length - 1) + "]";

                    resp.PagerSetup = pager;
                    resp.Data = responserecords;
                }
            }
            catch { resp.Data = new string[0, 0]; }

            resp.Response = new GenericResponse() { ResponseCode = "00" };
            return resp;
        }

        private static DataTable generateRecordTable(Component component, dynamic records, string pageIndex, string pageSize, int TotalRecordCount = 0)
        {
            DataTable result = new DataTable();

            foreach (Columns col in component.Columns)
            {
                result.Columns.Add(col.ColumnName);
            }

            foreach (var rec in records.ToArray())
            {
                var newRow = result.NewRow();
                foreach (var recCol in rec)
                {
                    try
                    {
                        newRow[recCol.Key] = recCol.Value;
                    }
                    catch { }
                }
                result.Rows.Add(newRow);
            }

            return result;
        }

        internal static async Task<GenericResponse> deleteRecord(Application Application, string url, QuickProcess.Model.deleteRecord_Model request, string ComponentName, UserSession UserSession, string RecordId)
        {
            System.Threading.Thread.CurrentThread.CurrentCulture = new System.Globalization.CultureInfo("en-GB");

            GenericResponse resp = new GenericResponse();
            var Component = await getComponent(Application, ComponentName);

            if (Component == null)
            {
                resp.ResponseCode = "404";
                resp.ResponseDescription = "Component \"" + ComponentName + "\" not found";
                return resp;
            }

            //ensure delete is enabled for datasource
            if (Component.EnableDelete == false)
            {
                resp.ResponseCode = ErrorCode.Forbidden;
                resp.ResponseDescription = "Forbidden";
                return resp;
            }

            if (Component.FetchMethod == "api" && string.IsNullOrEmpty(url) == false)
            {
                var postResponse = JsonConvert.DeserializeObject<GenericResponse>(await postUrl(url, Component.PostUrl, request));
                resp.ResponseCode = postResponse.ResponseCode;
                resp.Result = postResponse.Result;
                resp.ResponseDescription = postResponse.ResponseDescription;
                return resp;
            }


            string query = "delete from " + Component.DataSourceTable + " where " + Component.UniqueColumn.ColumnName + "=@" + Component.UniqueColumn.ColumnName;


            if (string.IsNullOrEmpty(Component.CustomDeleteQuery) == false)
            {
                query = Component.CustomDeleteQuery;
            }

            await ExecuteQuery(Component, query, UserSession, new SqlParameter("@" + Component.UniqueColumn.ColumnName, RecordId));

            resp.ResponseCode = "00";
            resp.ResponseDescription = "Record Deleted!";
            return resp;
        }

        internal static async Task<GenericResponse> getRecord(Application Application, string url, QuickProcess.Model.fetchRecord_Model request, string ComponentName, UserSession UserSession, string Dform, string RecordId, string DataSourceParams)
        {
            System.Threading.Thread.CurrentThread.CurrentCulture = new System.Globalization.CultureInfo("en-GB");


            GenericResponse resp = new GenericResponse();
            string query = "";
            ArrayList SqlParams = new ArrayList();
            bool containWhereClause = false;


            var Component = await getComponent(Application, ComponentName);

            if (Component == null)
            {
                resp.ResponseCode = "404";
                resp.ResponseDescription = "Component \"" + ComponentName + "\" not found";
                return resp;
            }

            if (Component.Type.ToLower() != "form")
            {
                resp.ResponseCode = ErrorCode.WrongEndpointCall;
                resp.ResponseDescription = "Wrong method endpoint call for Component: [" + ComponentName + "]";
                return resp;
            }

            if (Component.FetchMethod == "api" && string.IsNullOrEmpty(url) == false)
            {
                var postResponse = JsonConvert.DeserializeObject<GenericResponse>(await postUrl(url, Component.FetchUrl, request));
                resp.ResponseCode = postResponse.ResponseCode;
                resp.Result = postResponse.Result;
                resp.ResponseDescription = postResponse.ResponseDescription;
                return resp;
            }


            if (string.IsNullOrEmpty(RecordId))
            {
                query = "Select * from " + Component.DataSourceTable;
            }
            else
            {
                query = "Select * from " + Component.DataSourceTable + " where " + Component.UniqueColumn.ColumnName + "=@" + Component.UniqueColumn.ColumnName;
                SqlParams.Add(new SqlParameter("@" + Component.UniqueColumn.ColumnName, CastDataType(Component.UniqueColumn.DataType, RecordId)));
                containWhereClause = true;
            }



            //filter by parameter if any                
            if (string.IsNullOrEmpty(DataSourceParams) == false)
            {
                var filter = System.Text.Json.JsonSerializer.Deserialize<KeyValue[]>(DataSourceParams);


                foreach (var key_val in filter)
                {
                    foreach (var col in Component.Controls)
                    {
                        if (col.FieldName.ToLower() == key_val.Key.ToLower())
                        {
                            if (containWhereClause == false)
                            {
                                query += " where ";
                                containWhereClause = true;
                            }

                            if (query.EndsWith(" where ") == false)
                            {
                                query += " and ";
                            }

                            query += " [" + col.FieldName + "]=@" + col.FieldName.Replace(" ", "");
                            SqlParams.Add(new SqlParameter("@" + col.FieldName.Replace(" ", ""), key_val.Value));
                            break;
                        }
                    }
                }
            }


            int parameterIndex = 0;
            SqlParameter[] SqlParameters = new SqlParameter[SqlParams.Count];
            foreach (SqlParameter param in SqlParams)
            {
                SqlParameters[parameterIndex] = param;
                parameterIndex += 1;
            }


            if (Component.DataSourceType.ToLower() == "query")
                query = Component.CustomSelectQuery;


            DataTable record = (await ExecuteQuery(Component, query, UserSession, SqlParameters)).Tables[0];


            if (record.Rows.Count > 0)
            {
                foreach (FormControls ctr in Component.Controls)
                {
                    if (ctr.EnableFetch == false)
                        continue;

                    if (string.IsNullOrEmpty(ctr.FieldName) ==false && string.IsNullOrEmpty(record.Rows[0][ctr.FieldName].ToString()) == false)
                    {
                        //if (ctr.ControlType.ToLower() == ControlType.Dynamic_CheckList.ToLower())
                        //{
                        //    var list = await getLookupConnection(Component, UserSession, "", "", "");
                        //    string ctrVal = record.Rows[0][ctr.FieldName].ToString();
                        //    string valueList = "";

                        //    foreach (string val in ctrVal.Split(new char[] { ',' }))
                        //    {
                        //        var iteminfo = list.Where(item => item.Value == val).ToList();
                        //        if (iteminfo.Count() == 0)
                        //            throw new Exception("Unable to get record for field " + ctr.FieldName);

                        //        valueList += iteminfo[0].Key + ",";
                        //    }

                        //    if (valueList != "")
                        //        valueList = valueList.Substring(0, valueList.Length - 1);

                        //    ctr.Value = valueList;
                        //    continue;
                        //}


                        if (ctr.ControlType.ToLower() == ControlType.Date.ToLower() && ctr.Value != "")
                        {
                            try
                            {
                                if (Dform == "mdy")
                                {
                                    var date = DateTime.Parse(record.Rows[0][ctr.FieldName].ToString()).ToShortDateString();
                                    ctr.Value = date.Split(new char[] { '/' })[1] + "/" + date.Split(new char[] { '/' })[0] + "/" + date.Split(new char[] { '/' })[2];
                                }

                                if (Dform == "dmy")
                                {
                                    var date = DateTime.Parse(record.Rows[0][ctr.FieldName].ToString()).ToShortDateString();
                                    ctr.Value = date.Split(new char[] { '/' })[0] + "/" + date.Split(new char[] { '/' })[1] + "/" + date.Split(new char[] { '/' })[2];
                                }
                            }
                            catch { }
                            continue;
                        }


                        if (ctr.ControlType.ToLower() == ControlType.PASSWORD.ToLower() || ctr.ControlType.ToLower() == ControlType.ServerField.ToLower() || ctr.ControlType.ToLower() == ControlType.User_Password_Validator.ToLower() || ctr.ControlType.ToLower() == ControlType.User_PIN_Validator.ToLower())
                        {
                            ctr.Value = "**********";
                            ctr.DefaultValue = "";
                            continue;
                        }


                        if (ctr.ControlType.ToLower() == ControlType.User_Email_Validator.ToLower() || ctr.ControlType.ToLower() == ControlType.User_Phone_Validator.ToLower())
                        {
                            ctr.Value = "";
                            ctr.DefaultValue = "";
                            continue;
                        }


                        if (ctr.ControlType.ToLower() == ControlType.CheckBox.ToLower())
                        {
                            ctr.Value = record.Rows[0][ctr.FieldName].ToString();

                            if (string.IsNullOrEmpty(ctr.Value))
                                ctr.DefaultValue = false.ToString();

                            if (string.IsNullOrEmpty(ctr.DefaultValue))
                                ctr.DefaultValue = false.ToString();

                            continue;
                        }

                        //this snippet handles for all other input types
                        ctr.Value = record.Rows[0][ctr.FieldName].ToString();

                    }
                }
            }

            //remove server inputs
            Component.Controls = Component.Controls.Where(ctr => ctr.ControlType.ToLower() != ControlType.ServerField.ToLower() && ctr.ControlType.ToLower() != ControlType.GUID.ToLower()).ToArray();

            dynamic values = new Newtonsoft.Json.Linq.JObject();
            foreach (var val in Component.Controls)
            {
                values[val.FieldName] = val.Value;
            }

            resp.ResponseCode = "00";
            resp.Result = Newtonsoft.Json.JsonConvert.SerializeObject(values);
            return resp;
        }

        internal static async Task<GenericResponse> saveRecord(Application Application, string url, QuickProcess.Model.saveRecord_Model request, string ComponentName, UserSession UserSession, string Dform, string FormInfo)
        {
            GenericResponse resp = new GenericResponse();
            DataTable RequestInformationTable = new DataTable();
            List<SqlParameter> SqlParameters = new List<SqlParameter>();
            string query = "";
            string values = "";
            int Index = 0;
            string RecordId = "";

            var Component = await getComponent(Application, ComponentName);

            if (Component == null)
            {
                resp.ResponseCode = "404";
                resp.ResponseDescription = "Component \"" + ComponentName + "\" not found";
                return resp;
            }

            if (Component.Type.ToLower() != "form")
            {
                resp.ResponseCode = ErrorCode.WrongEndpointCall;
                resp.ResponseDescription = "Wrong method endpoint call for Component: [" + ComponentName + "]";
                return resp;
            }

            if (Component.PostMethod == "api" && string.IsNullOrEmpty(url) == false)
            {
                var postResponse = JsonConvert.DeserializeObject<GenericResponse>(await postUrl(url, Component.PostUrl, request));
                resp.ResponseCode = postResponse.ResponseCode;
                resp.Result = postResponse.Result;
                resp.ResponseDescription = postResponse.ResponseDescription;
                return resp;
            }




            //transfer values of form submitted to form structure retrieved from db
            //validate compulsory inputs 
            var ObjParams = Newtonsoft.Json.Linq.JToken.Parse(FormInfo);
            Dictionary<string, string> dictObj = ObjParams.ToObject<Dictionary<string, string>>();




            foreach (var p in dictObj.Keys.ToArray())
            {
                if (dictObj[p] != null)
                {
                    var ctr = Component.Controls.Where(control => control.FieldName == p).ToList()[0];
                    ctr.Value = dictObj[p].ToString();


                    if ((ctr.ControlType.ToLower() == ControlType.Number_CommaSeparated.ToLower() || ctr.ControlType.ToLower() == ControlType.Money.ToLower()) && string.IsNullOrEmpty(ctr.Value) == false)
                    {
                        ctr.Value = ctr.Value.ToString().Replace(",", "");
                    }

                    //validate static dropdown selected item
                    if ((ctr.ControlType.ToLower() == ControlType.Static_DropDown.ToLower() || ctr.ControlType.ToLower() == ControlType.Static_CheckList.ToLower() || ctr.ControlType.ToLower() == ControlType.Static_ImageLookup.ToLower()) && string.IsNullOrEmpty(ctr.Value) == false)
                    {
                        if (ctr.List != null)
                        {
                            if (ctr.List.Length > 0)
                            {
                                //validate option
                                if (ctr.List.Where(option => option.Value == ctr.Value).ToList().Count == 0)
                                {
                                    resp.ResponseCode = "-1";
                                    resp.ResponseDescription = "Invalid option selected for " + ctr.Title;
                                    return resp;
                                }
                            }
                        }
                    }
                }
            }


            //give default value to server field controls on submitted form
            Component.Controls.Where(item => item.ControlType.ToLower() == ControlType.ServerField.ToLower()).ToList().ForEach(ctr => ctr.Value = ctr.DefaultValue);


            //give ip address to ipAddress controls on submitted form
            Component.Controls.Where(item => item.ControlType.ToLower() == ControlType.IPAddress.ToLower()).ToList().ForEach(ctr => ctr.Value = GetClientIp());


            //get index where control bound to primary column is
            int PrimaryColumnIndex = getFormUniqueColumnIndex(Component);
            string PrimryColumnValue = "";

            if (PrimaryColumnIndex != -1)
                PrimryColumnValue = Component.Controls[PrimaryColumnIndex].Value;



            //save/update records
            if (string.IsNullOrEmpty(PrimryColumnValue))
            {
                //ensure insert is enabled for datasource
                if (Component.EnableInsert == false)
                {
                    resp.ResponseCode = "403";
                    resp.ResponseDescription = "Operation not allowed";
                    return resp;
                }

                query = "Insert into " + Component.DataSourceTable + " (";


                foreach (FormControls ctr in Component.Controls)
                {
                    //skip control if its identity or readonly or insert is disabled for control
                    if (PrimaryColumnIndex != -1)
                    {
                        if (ctr.FieldName.ToLower() == Component.Controls[PrimaryColumnIndex].FieldName.ToLower() || ctr.ReadOnly || ctr.EnableInsert == false || ctr.EnableSave == false)
                        {
                            continue;
                        }
                    }

                    if ((ctr.ControlType.ToLower() == ControlType.Text.ToLower() || ctr.ControlType.ToLower() == ControlType.TextArea.ToLower() || ctr.ControlType.ToLower() == ControlType.Rating.ToLower() || ctr.ControlType.ToLower() == ControlType.ToggleList.ToLower() || ctr.ControlType.ToLower() == ControlType.Toggle.ToLower() || ctr.ControlType.ToLower() == ControlType.Static_CheckList.ToLower() || ctr.ControlType.ToLower() == ControlType.RadioButtonList.ToLower() || ctr.ControlType.ToLower() == ControlType.SignPad.ToLower() || ctr.ControlType.ToLower() == ControlType.User_Phone_Validator.ToLower() || ctr.ControlType.ToLower() == ControlType.User_Email_Validator.ToLower() || ctr.ControlType.ToLower() == ControlType.IPAddress.ToLower() || ctr.ControlType.ToLower() == ControlType.Geolocation.ToLower() || ctr.ControlType.ToLower() == ControlType.HtmlEditor.ToLower() || ctr.ControlType.ToLower() == ControlType.ServerField.ToLower() || ctr.ControlType.ToLower() == ControlType.Phone.ToLower() || ctr.ControlType.ToLower() == ControlType.Month.ToLower() || ctr.ControlType.ToLower() == ControlType.Week.ToLower() || ctr.ControlType.ToLower() == ControlType.ColourPicker.ToLower() || ctr.ControlType.ToLower() == ControlType.Range.ToLower() || ctr.ControlType.ToLower() == ControlType.LABEL.ToLower() || ctr.ControlType.ToLower() == ControlType.HIDDEN_FIELD.ToLower() || ctr.ControlType.ToLower() == ControlType.Static_DropDown.ToLower() || ctr.ControlType.ToLower() == ControlType.Static_ImageLookup.ToLower() || ctr.ControlType.ToLower() == ControlType.Text_Area.ToLower() || ctr.ControlType.ToLower() == ControlType.StaffLookUp.ToLower() || ctr.ControlType.ToLower() == ControlType.FILEUPLOAD.ToLower() || ctr.ControlType.ToLower() == ControlType.EMAIL.ToLower()))
                    {
                        query += "[" + ctr.FieldName + "],";
                        values += "@" + ctr.FieldName.Replace(" ", "") + ",";

                        if (string.IsNullOrEmpty(ctr.Value))
                        {
                            SqlParameters.Add(new SqlParameter("@" + ctr.FieldName.Replace(" ", ""), DBNull.Value));
                        }
                        else
                        {
                            SqlParameters.Add(new SqlParameter("@" + ctr.FieldName.Replace(" ", ""), Encrypt_Hash(ctr.DataForm, CastDataType(ctr.DataType, ctr.Value))));
                        }
                    }

                    if ((ctr.ControlType.ToLower() == ControlType.CheckBox.ToLower()))
                    {
                        query += "[" + ctr.FieldName + "],";
                        values += "@" + ctr.FieldName.Replace(" ", "") + ",";

                        if (string.IsNullOrEmpty(ctr.Value))
                        {
                            SqlParameters.Add(new SqlParameter("@" + ctr.FieldName.Replace(" ", ""), false));
                        }
                        else
                        {
                            SqlParameters.Add(new SqlParameter("@" + ctr.FieldName.Replace(" ", ""), bool.Parse(ctr.Value)));
                        }
                    }

                    if (ctr.ControlType.ToLower() == ControlType.GUID.ToLower())
                    {
                        query += "[" + ctr.FieldName + "],";
                        values += "@" + ctr.FieldName.Replace(" ", "") + ",";

                        SqlParameters.Add(new SqlParameter("@" + ctr.FieldName.Replace(" ", ""), Encrypt_Hash(ctr.DataForm, Guid.NewGuid().ToString())));
                    }

                    if (ctr.ControlType.ToLower() == ControlType.RandomNumber.ToLower() && ctr.EnableUpdate == true)
                    {
                        query += "[" + ctr.FieldName + "],";
                        values += "@" + ctr.FieldName.Replace(" ", "") + ",";

                        var range = ctr.Value.Split(new char[] { ':' });
                        var randomNumber = new Random().Next(int.Parse(range[0]), int.Parse(range[1]));

                        SqlParameters.Add(new SqlParameter("@" + ctr.FieldName.Replace(" ", ""), Encrypt_Hash(ctr.DataForm, randomNumber)));
                    }

                    if ((ctr.ControlType.ToLower() == ControlType.Number.ToLower() || ctr.ControlType.ToLower() == ControlType.Number_CommaSeparated.ToLower() || ctr.ControlType.ToLower() == ControlType.Money.ToLower()))
                    {
                        query += "[" + ctr.FieldName + "],";
                        values += "@" + ctr.FieldName.Replace(" ", "") + ",";

                        if (string.IsNullOrEmpty(ctr.Value))
                        {
                            SqlParameters.Add(new SqlParameter("@" + ctr.FieldName.Replace(" ", ""), DBNull.Value));
                        }
                        else
                        {
                            SqlParameters.Add(new SqlParameter("@" + ctr.FieldName.Replace(" ", ""), Encrypt_Hash(ctr.DataForm, CastDataType(ctr.DataType, ctr.Value.Replace(",", "")))));
                        }
                    }

                    if ((ctr.ControlType.ToLower() == ControlType.PASSWORD.ToLower() || ctr.ControlType.ToLower() == ControlType.User_Password_Validator.ToLower() || ctr.ControlType.ToLower() == ControlType.User_PIN_Validator.ToLower()))
                    {
                        query += "[" + ctr.FieldName + "],";
                        values += "@" + ctr.FieldName.Replace(" ", "") + ",";

                        if (string.IsNullOrEmpty(ctr.Value))
                        {
                            SqlParameters.Add(new SqlParameter("@" + ctr.FieldName.Replace(" ", ""), DBNull.Value));
                        }
                        else
                        {
                            SqlParameters.Add(new SqlParameter("@" + ctr.FieldName.Replace(" ", ""), Encrypt_Hash(ctr.DataForm, ctr.Value)));
                        }
                    }

                    if (ctr.ControlType.ToLower() == ControlType.CONST_USERNAME.ToLower())
                    {
                        query += "[" + ctr.FieldName + "],";
                        values += "@" + ctr.FieldName.Replace(" ", "") + ",";
                        SqlParameters.Add(new SqlParameter("@" + ctr.FieldName.Replace(" ", ""), Encrypt_Hash(ctr.DataForm, UserSession.UserName)));
                    }

                    if (ctr.ControlType.ToLower() == ControlType.CONST_SESSIONID.ToLower())
                    {
                        query += "[" + ctr.FieldName + "],";
                        values += "@" + ctr.FieldName.Replace(" ", "") + ",";
                        SqlParameters.Add(new SqlParameter("@" + ctr.FieldName.Replace(" ", ""), Encrypt_Hash(ctr.DataForm, UserSession.SessionID)));
                    }

                    if (ctr.ControlType.ToLower() == ControlType.CONST_TENANTID.ToLower())
                    {
                        query += "[" + ctr.FieldName + "],";
                        values += "@" + ctr.FieldName.Replace(" ", "") + ",";
                        SqlParameters.Add(new SqlParameter("@" + ctr.FieldName.Replace(" ", ""), Encrypt_Hash(ctr.DataForm, UserSession.TenantID)));
                    }

                    if (ctr.ControlType.ToLower() == ControlType.CurrentDateTime.ToLower())
                    {
                        query += "[" + ctr.FieldName + "],";
                        values += "@" + ctr.FieldName.Replace(" ", "") + ",";

                        SqlParameters.Add(new SqlParameter("@" + ctr.FieldName.Replace(" ", ""), Encrypt_Hash(ctr.DataForm, DateTime.Now)));
                    }

                    if ((ctr.ControlType.ToLower() == ControlType.Date.ToLower() || ctr.ControlType.ToLower() == ControlType.DateTime.ToLower()))
                    {
                        query += "[" + ctr.FieldName + "],";
                        values += "@" + ctr.FieldName.Replace(" ", "") + ",";
                        if (string.IsNullOrEmpty(ctr.Value))
                        {
                            SqlParameters.Add(new SqlParameter("@" + ctr.FieldName.Replace(" ", ""), DBNull.Value));
                        }
                        else
                        {
                            if (Dform == "mdy")
                            {
                                string date = ctr.Value.Split(new char[] { '/' })[1] + "/" + ctr.Value.Split(new char[] { '/' })[0] + "/" + ctr.Value.Split(new char[] { '/' })[2];
                                SqlParameters.Add(new SqlParameter("@" + ctr.FieldName.Replace(" ", ""), Encrypt_Hash(ctr.DataForm, DateTime.Parse(date))));
                            }
                            else
                            {
                                SqlParameters.Add(new SqlParameter("@" + ctr.FieldName.Replace(" ", ""), Encrypt_Hash(ctr.DataForm, DateTime.Parse(ctr.Value))));
                            }
                        }
                    }

                    if (string.IsNullOrEmpty(ctr.DataSource) == false)
                    {
                        if (ctr.ControlType.ToLower() == ControlType.AutoComplete.ToLower())
                        {
                            if (string.IsNullOrEmpty(ctr.Value))
                            {
                                SqlParameters.Add(new SqlParameter("@" + ctr.FieldName.Replace(" ", ""), DBNull.Value));
                            }
                            else
                            {
                                //var list = await getLookupConnection(await getComponent(Application, ctr.DataSource), UserSession, "", "", "");
                                //var keyvalue = list.Where(item => item.Key == ctr.Value).ToList();
                                //if (keyvalue.Count() == 0)
                                //{
                                //    resp.ResponseCode = "-1";
                                //    resp.ResponseDescription = "Invalid option selected for " + ctr.Title;
                                //    return resp;
                                //}

                                //query += ctr.FieldName + ",";
                                //values += "@" + ctr.FieldName.Replace(" ", "") + ",";
                                //SqlParameters.Add(new SqlParameter("@" + ctr.FieldName.Replace(" ", ""), Encrypt_Hash(ctr.DataForm, CastDataType(ctr.DataType, keyvalue[0].Value))));


                                query += ctr.FieldName + ",";
                                values += "@" + ctr.FieldName.Replace(" ", "") + ",";
                                SqlParameters.Add(new SqlParameter("@" + ctr.FieldName.Replace(" ", ""), Encrypt_Hash(ctr.DataForm, CastDataType(ctr.DataType, ctr.Value))));

                            }
                        }

                        if (ctr.ControlType.ToLower() == ControlType.Dynamic_DropDown.ToLower())
                        {
                            if (string.IsNullOrEmpty(ctr.Value))
                            {
                                SqlParameters.Add(new SqlParameter("@" + ctr.FieldName.Replace(" ", ""), DBNull.Value));
                            }
                            else
                            {
                                //var list = await getLookupConnection(await getComponent(ctr.DataSource, AppId, ConnectionList), UserSession, ConnectionList, "", "");

                                //if (list.Where(item => item.Value == ctr.Value).ToList().Count == 0)
                                //{
                                //    resp.ResponseCode = "-1";
                                //    resp.ResponseDescription = "Invalid option selected for " + ctr.Title;
                                //    return resp;
                                //}

                                query += "[" + ctr.FieldName + "],";
                                values += "@" + ctr.FieldName.Replace(" ", "") + ",";
                                SqlParameters.Add(new SqlParameter("@" + ctr.FieldName.Replace(" ", ""), Encrypt_Hash(ctr.DataForm, CastDataType(ctr.DataType, ctr.Value))));
                            }
                        }

                        if (ctr.ControlType.ToLower() == ControlType.Dynamic_ImageLookup.ToLower())
                        {
                            if (string.IsNullOrEmpty(ctr.Value))
                            {
                                SqlParameters.Add(new SqlParameter("@" + ctr.FieldName.Replace(" ", ""), DBNull.Value));
                            }
                            else
                            {
                                var list = await getLookupConnection(await getComponent(Application, ctr.DataSource), UserSession, "", "", "");

                                if (list.Where(item => item.Value == ctr.Value).ToList().Count == 0)
                                {
                                    resp.ResponseCode = "-1";
                                    resp.ResponseDescription = "Invalid option selected for " + ctr.Title;
                                    return resp;
                                }

                                query += "[" + ctr.FieldName + "],";
                                values += "@" + ctr.FieldName.Replace(" ", "") + ",";
                                SqlParameters.Add(new SqlParameter("@" + ctr.FieldName.Replace(" ", ""), Encrypt_Hash(ctr.DataForm, CastDataType(ctr.DataType, ctr.Value))));
                            }
                        }

                        if (ctr.ControlType.ToLower() == ControlType.TABLE_LOOKUP.ToLower())
                        {
                            if (string.IsNullOrEmpty(ctr.Value))
                            {
                                SqlParameters.Add(new SqlParameter("@" + ctr.FieldName.Replace(" ", ""), DBNull.Value));
                            }
                            else
                            {
                                var list = await getLookupConnection(await getComponent(Application, ctr.DataSource), UserSession, "", "", "");
                                var keyvalue = list.Where(item => item.Key == ctr.Value).ToList();
                                if (keyvalue.Count() == 0)
                                {
                                    resp.ResponseCode = "-1";
                                    resp.ResponseDescription = "Invalid option selected for " + ctr.Title;
                                    return resp;
                                }

                                query += "[" + ctr.FieldName + "],";
                                values += "@" + ctr.FieldName.Replace(" ", "") + ",";
                                SqlParameters.Add(new SqlParameter("@" + ctr.FieldName.Replace(" ", ""), Encrypt_Hash(ctr.DataForm, CastDataType(ctr.DataType, keyvalue[0].Value))));
                            }
                        }

                        if (ctr.ControlType.ToLower() == ControlType.Dynamic_CheckList.ToLower())
                        {
                            query += "[" + ctr.FieldName + "],";
                            values += "@" + ctr.FieldName.Replace(" ", "") + ",";

                            if (string.IsNullOrEmpty(ctr.Value))
                            {
                                SqlParameters.Add(new SqlParameter("@" + ctr.FieldName.Replace(" ", ""), DBNull.Value));
                            }
                            else
                            {
                                //get datasource for control in datasource collection of current process
                                //var list = await getLookupConnection(await getComponent(Application, ctr.DataSource), UserSession, "", "", "");
                                //string valueFieldParamValues = "";
                                //foreach (string key in ctr.Value.Split(new char[] { ',' }))
                                //{
                                //    var keyvalue = list.Where(item => item.Key == key).ToList();
                                //    if (keyvalue.Count() == 0)
                                //    {
                                //        resp.ResponseCode = "-1";
                                //        resp.ResponseDescription = "Invalid option selected for " + ctr.Title;
                                //        return resp;
                                //    }

                                //    //generate comma separated valuefields
                                //    valueFieldParamValues += keyvalue[0].Value + ",";
                                //}

                                //if (valueFieldParamValues != "")
                                //    valueFieldParamValues = valueFieldParamValues.Substring(0, valueFieldParamValues.Length - 1);

                                //SqlParameters.Add(new SqlParameter("@" + ctr.FieldName.Replace(" ", ""), Encrypt_Hash(ctr.DataForm, CastDataType(ctr.DataType, valueFieldParamValues))));

                                query += "[" + ctr.FieldName + "],";
                                values += "@" + ctr.FieldName.Replace(" ", "") + ",";
                                SqlParameters.Add(new SqlParameter("@" + ctr.FieldName.Replace(" ", ""), Encrypt_Hash(ctr.DataForm, CastDataType(ctr.DataType, ctr.Value))));
                            }
                        }
                    }
                }


                //append query to get identity/auto increment column
                switch (Component.Connection.Engine.ToLower())
                {
                    case "ms sqlserver":
                        query = query.Substring(0, query.Length - 1) + ") values(" + values.Substring(0, values.Length - 1) + ")  select * from " + Component.DataSourceTable + " where " + Component.UniqueColumn.ColumnName + "= scope_identity()";
                        break;

                    case "oracle":
                        query = query.Substring(0, query.Length - 1) + ") values(" + values.Substring(0, values.Length - 1) + "); variable Inserted_Value number; returning " + Component.UniqueColumn.ColumnName + " into :Inserted_Value; select :Inserted_Value from DUAL;   select  * from " + Component.DataSourceTable + " where " + Component.UniqueColumn.ColumnName + "= Inserted_Value; ";
                        //query = query.Substring(0, query.Length - 1) + ") values(" + values.Substring(0, values.Length - 1) + "); variable Inserted_Value number; returning " + DataSource.PrimaryColumn.ColumnName + " into :Inserted_Value; select :Inserted_Value from DUAL ";
                        break;

                    case "my sql":
                        query = query.Substring(0, query.Length - 1) + ") values(" + values.Substring(0, values.Length - 1) + ");  select  * from " + Component.DataSourceTable + " where " + Component.UniqueColumn.ColumnName + "= LAST_INSERT_ID();";
                        break;

                    case "postgre sql":
                        query = query.Substring(0, query.Length - 1) + ") values(" + values.Substring(0, values.Length - 1) + ")   " + ((Component.UniqueColumn != null) ? " RETURNING * " : "") + ";";
                        // query = query.Substring(0, query.Length - 1) + ") values(" + values.Substring(0, values.Length - 1) + ")   " + ((DataSource.PrimaryColumn != null) ? " RETURNING " + DataSource.PrimaryColumn.ColumnName : "") + ";";
                        break;
                }
            }
            else
            {
                //this code section excutes record-update. ensure update is enabled for datasource
                if (Component.EnableEdit == false)
                {
                    resp.ResponseCode = "-1";
                    resp.ResponseDescription = "Operation not allowed";
                    return resp;
                }

                DataTable RecordInDB = new DataTable();

                //get current record in db. Note: if datasourcetype is query, there will be no uniquecolmn
                if (PrimaryColumnIndex != -1 && Component.DataSourceType.ToLower() != "query")
                    RecordInDB = (await ExecuteQuery(Component, "select * from " + Component.DataSourceTable + " where [" + Component.UniqueColumn.ColumnName + "]=@" + Component.UniqueColumn.ColumnName, UserSession, new SqlParameter("@" + Component.UniqueColumn.ColumnName, CastDataType(Component.UniqueColumn.DataType, Component.Controls[PrimaryColumnIndex].Value)))).Tables[0];


                query = "Update  " + Component.DataSourceTable + " set ";

                foreach (FormControls ctr in Component.Controls)
                {
                    if (ctr.FieldName == Component.UniqueColumn.ColumnName)
                    {
                        RecordId = ctr.Value;
                        SqlParameters.Add(new SqlParameter("@" + ctr.FieldName.Replace(" ", ""), CastDataType(Component.UniqueColumn.DataType, ctr.Value)));
                        continue;
                    }


                    //overwrite modification sent from user by record in db for readonly/identity/noupdate inputs
                    if (RecordInDB.Rows.Count > 0)
                    {
                        if (ctr.FieldName.ToLower() == Component.Controls[PrimaryColumnIndex].FieldName.ToLower() || ctr.ReadOnly || ctr.EnableSave == false || ctr.EnableUpdate == false)
                        {
                            //populate value from db
                            ctr.Value = RecordInDB.Rows[0][ctr.FieldName].ToString();
                            if (string.IsNullOrEmpty(ctr.Value) == false)
                                SqlParameters.Add(new SqlParameter("@" + ctr.FieldName.Replace(" ", ""), CastDataType(ctr.DataType, ctr.Value)));
                            else
                                SqlParameters.Add(new SqlParameter("@" + ctr.FieldName.Replace(" ", ""), DBNull.Value));

                            continue;
                        }
                    }


                    if ((ctr.ControlType.ToLower() == ControlType.Text.ToLower() || ctr.ControlType.ToLower() == ControlType.TextArea.ToLower() || ctr.ControlType.ToLower() == ControlType.Rating.ToLower() || ctr.ControlType.ToLower() == ControlType.ToggleList.ToLower() || ctr.ControlType.ToLower() == ControlType.Toggle.ToLower() || ctr.ControlType.ToLower() == ControlType.Static_CheckList.ToLower() || ctr.ControlType.ToLower() == ControlType.RadioButtonList.ToLower() || ctr.ControlType.ToLower() == ControlType.SignPad.ToLower() || ctr.ControlType.ToLower() == ControlType.User_Phone_Validator.ToLower() || ctr.ControlType.ToLower() == ControlType.User_Email_Validator.ToLower() || ctr.ControlType.ToLower() == ControlType.IPAddress.ToLower() || ctr.ControlType.ToLower() == ControlType.Geolocation.ToLower() || ctr.ControlType.ToLower() == ControlType.HtmlEditor.ToLower() || ctr.ControlType.ToLower() == ControlType.ServerField.ToLower() || ctr.ControlType.ToLower() == ControlType.Phone.ToLower() || ctr.ControlType.ToLower() == ControlType.Month.ToLower() || ctr.ControlType.ToLower() == ControlType.Week.ToLower() || ctr.ControlType.ToLower() == ControlType.ColourPicker.ToLower() || ctr.ControlType.ToLower() == ControlType.Range.ToLower() || ctr.ControlType.ToLower() == ControlType.LABEL.ToLower() || ctr.ControlType.ToLower() == ControlType.HIDDEN_FIELD.ToLower() || ctr.ControlType.ToLower() == ControlType.Static_DropDown.ToLower() || ctr.ControlType.ToLower() == ControlType.Static_ImageLookup.ToLower() || ctr.ControlType.ToLower() == ControlType.Text_Area.ToLower() || ctr.ControlType.ToLower() == ControlType.StaffLookUp.ToLower() || ctr.ControlType.ToLower() == ControlType.FILEUPLOAD.ToLower() || ctr.ControlType.ToLower() == ControlType.EMAIL.ToLower()) && ctr.EnableUpdate == true)
                    {
                        if (string.IsNullOrEmpty(ctr.Value) == false)
                        {
                            query += "[" + ctr.FieldName + "]=@" + ctr.FieldName.Replace(" ", "") + ",";
                            SqlParameters.Add(new SqlParameter("@" + ctr.FieldName.Replace(" ", ""), Encrypt_Hash(ctr.DataForm, CastDataType(ctr.DataType, ctr.Value))));
                        }
                        else
                        {
                            ctr.Value = DBNull.Value.ToString();
                            query += "[" + ctr.FieldName + "]=@" + ctr.FieldName.Replace(" ", "") + ",";
                            SqlParameters.Add(new SqlParameter("@" + ctr.FieldName.Replace(" ", ""), DBNull.Value));
                        }
                    }

                    if ((ctr.ControlType.ToLower() == ControlType.CheckBox.ToLower()))
                    {
                        query += "[" + ctr.FieldName + "]=@" + ctr.FieldName.Replace(" ", "") + ",";

                        if (string.IsNullOrEmpty(ctr.Value))
                        {
                            SqlParameters.Add(new SqlParameter("@" + ctr.FieldName.Replace(" ", ""), false));
                        }
                        else
                        {
                            SqlParameters.Add(new SqlParameter("@" + ctr.FieldName.Replace(" ", ""), bool.Parse(ctr.Value)));
                        }
                    }

                    if (ctr.ControlType.ToLower() == ControlType.GUID.ToLower() && ctr.EnableUpdate == true)
                    {
                        query += "[" + ctr.FieldName + "]=@" + ctr.FieldName.Replace(" ", "") + ",";
                        SqlParameters.Add(new SqlParameter("@" + ctr.FieldName.Replace(" ", ""), Encrypt_Hash(ctr.DataForm, Guid.NewGuid().ToString())));
                    }

                    if (ctr.ControlType.ToLower() == ControlType.RandomNumber.ToLower() && ctr.EnableUpdate == true)
                    {
                        query += "[" + ctr.FieldName + "]=@" + ctr.FieldName.Replace(" ", "") + ",";
                        var range = ctr.Value.Split(new char[] { ':' });
                        var randomNumber = new Random().Next(int.Parse(range[0]), int.Parse(range[1]));
                        SqlParameters.Add(new SqlParameter("@" + ctr.FieldName.Replace(" ", ""), Encrypt_Hash(ctr.DataForm, randomNumber)));
                    }


                    //check if password was changed hence the masked value would have been updated otherwise simply skip as the password field was not changed
                    if ((ctr.ControlType.ToLower() == ControlType.PASSWORD.ToLower() || ctr.ControlType.ToLower() == ControlType.User_Password_Validator.ToLower() || ctr.ControlType.ToLower() == ControlType.User_PIN_Validator.ToLower()) && ctr.Value != "**********" && ctr.EnableUpdate == true)
                    {
                        if (string.IsNullOrEmpty(ctr.Value) == false)
                        {
                            query += "[" + ctr.FieldName + "]=@" + ctr.FieldName.Replace(" ", "") + ",";
                            SqlParameters.Add(new SqlParameter("@" + ctr.FieldName.Replace(" ", ""), Encrypt_Hash(ctr.DataForm, ctr.Value)));
                        }
                        else
                        {
                            ctr.Value = DBNull.Value.ToString();
                            query += "[" + ctr.FieldName + "]=@" + ctr.FieldName.Replace(" ", "") + ",";
                            SqlParameters.Add(new SqlParameter("@" + ctr.FieldName.Replace(" ", ""), DBNull.Value));
                        }
                    }

                    if (ctr.ControlType.ToLower() == ControlType.CurrentDateTime.ToLower() && ctr.EnableUpdate == true)
                    {
                        query += "[" + ctr.FieldName + "]=@" + ctr.FieldName.Replace(" ", "") + ",";
                        SqlParameters.Add(new SqlParameter("@" + ctr.FieldName.Replace(" ", ""), Encrypt_Hash(ctr.DataForm, DateTime.Now)));
                    }

                    if ((ctr.ControlType.ToLower() == ControlType.Number.ToLower() || ctr.ControlType.ToLower() == ControlType.Number_CommaSeparated.ToLower() || ctr.ControlType.ToLower() == ControlType.Money.ToLower()) && ctr.EnableUpdate == true)
                    {
                        if (string.IsNullOrEmpty(ctr.Value) == false)
                        {
                            query += "[" + ctr.FieldName + "]=@" + ctr.FieldName.Replace(" ", "") + ",";
                            SqlParameters.Add(new SqlParameter("@" + ctr.FieldName.Replace(" ", ""), Encrypt_Hash(ctr.DataForm, CastDataType(ctr.DataType, ctr.Value.Replace(",", "")))));
                        }
                        else
                        {
                            ctr.Value = DBNull.Value.ToString();
                            query += "[" + ctr.FieldName + "]=@" + ctr.FieldName.Replace(" ", "") + ",";
                            SqlParameters.Add(new SqlParameter("@" + ctr.FieldName.Replace(" ", ""), DBNull.Value));
                        }
                    }

                    if ((ctr.ControlType.ToLower() == ControlType.Date.ToLower() || ctr.ControlType.ToLower() == ControlType.DateTime.ToLower()) && string.IsNullOrEmpty(ctr.Value) == false && ctr.EnableUpdate == true)
                    {
                        if (string.IsNullOrEmpty(ctr.Value) == false)
                        {
                            query += "[" + ctr.FieldName + "]=@" + ctr.FieldName.Replace(" ", "") + ",";

                            if (Dform == "mdy")
                            {
                                string date = ctr.Value.Split(new char[] { '/' })[1] + "/" + ctr.Value.Split(new char[] { '/' })[0] + "/" + ctr.Value.Split(new char[] { '/' })[2];
                                SqlParameters.Add(new SqlParameter("@" + ctr.FieldName.Replace(" ", ""), Encrypt_Hash(ctr.DataForm, DateTime.Parse(date))));
                            }
                            else
                            {
                                SqlParameters.Add(new SqlParameter("@" + ctr.FieldName.Replace(" ", ""), Encrypt_Hash(ctr.DataForm, DateTime.Parse(ctr.Value))));
                            }
                        }
                        else
                        {
                            ctr.Value = DBNull.Value.ToString();
                            query += "[" + ctr.FieldName + "]=@" + ctr.FieldName.Replace(" ", "") + ",";
                            SqlParameters.Add(new SqlParameter("@" + ctr.FieldName.Replace(" ", ""), DBNull.Value));
                        }
                    }

                    if (ctr.ControlType.ToLower() == ControlType.CONST_USERNAME.ToLower() && ctr.EnableUpdate == true)
                    {
                        query += "[" + ctr.FieldName + "]=@" + ctr.FieldName.Replace(" ", "") + ",";
                        SqlParameters.Add(new SqlParameter("@" + ctr.FieldName.Replace(" ", ""), Encrypt_Hash(ctr.DataForm, UserSession.UserName)));
                    }

                    if (ctr.ControlType.ToLower() == ControlType.CONST_SESSIONID.ToLower() && ctr.EnableUpdate == true)
                    {
                        query += "[" + ctr.FieldName + "]=@" + ctr.FieldName.Replace(" ", "") + ",";
                        SqlParameters.Add(new SqlParameter("@" + ctr.FieldName.Replace(" ", ""), Encrypt_Hash(ctr.DataForm, UserSession.SessionID)));
                    }

                    if (ctr.ControlType.ToLower() == ControlType.CONST_TENANTID.ToLower() && ctr.EnableUpdate == true)
                    {
                        query += "[" + ctr.FieldName + "]=@" + ctr.FieldName.Replace(" ", "") + ",";
                        SqlParameters.Add(new SqlParameter("@" + ctr.FieldName.Replace(" ", ""), Encrypt_Hash(ctr.DataForm, UserSession.TenantID)));
                    }

                    if (string.IsNullOrEmpty(ctr.DataSource) == false)
                    {
                        if ((ctr.ControlType.ToLower() == ControlType.AutoComplete.ToLower()) && ctr.EnableUpdate == true)
                        {
                            if (string.IsNullOrEmpty(ctr.Value) == false)
                            {
                                //var list = await getLookupConnection(await getComponent(Application, ctr.DataSource), UserSession, "", "", "");
                                //var keyvalue = list.Where(item => item.Key == ctr.Value).ToList();
                                //if (keyvalue.Count() == 0)
                                //{
                                //    resp.ResponseCode = "-1";
                                //    resp.ResponseDescription = "Invalid option selected for " + ctr.Title;
                                //    return resp;
                                //}

                                //query += "[" + ctr.FieldName + "]=@" + ctr.FieldName.Replace(" ", "") + ",";
                                //SqlParameters.Add(new SqlParameter("@" + ctr.FieldName.Replace(" ", ""), Encrypt_Hash(ctr.DataForm, CastDataType(ctr.DataType, keyvalue[0].Value))));

                                query += "[" + ctr.FieldName + "]=@" + ctr.FieldName.Replace(" ", "") + ",";
                                SqlParameters.Add(new SqlParameter("@" + ctr.FieldName.Replace(" ", ""), Encrypt_Hash(ctr.DataForm, CastDataType(ctr.DataType, ctr.Value))));
                            }
                            else
                            {
                                //clear information
                                ctr.Value = DBNull.Value.ToString();
                                query += "[" + ctr.FieldName + "]=@" + ctr.FieldName.Replace(" ", "") + ",";
                                SqlParameters.Add(new SqlParameter("@" + ctr.FieldName.Replace(" ", ""), DBNull.Value));
                            }

                        }

                        if (ctr.ControlType.ToLower() == ControlType.TABLE_LOOKUP.ToLower() && ctr.EnableUpdate == true)
                        {
                            if (string.IsNullOrEmpty(ctr.Value) == false)
                            {
                                var list = await getLookupConnection(await getComponent(Application, ctr.DataSource), UserSession, "", "", "");
                                var keyvalue = list.Where(item => item.Key == ctr.Value).ToList();
                                if (keyvalue.Count() == 0)
                                {
                                    resp.ResponseCode = "-1";
                                    resp.ResponseDescription = "Invalid option selected for " + ctr.Title;
                                    return resp;
                                }

                                query += "[" + ctr.FieldName + "]=@" + ctr.FieldName.Replace(" ", "") + ",";
                                SqlParameters.Add(new SqlParameter("@" + ctr.FieldName.Replace(" ", ""), Encrypt_Hash(ctr.DataForm, CastDataType(ctr.DataType, keyvalue[0].Value))));
                            }
                            else
                            {
                                //clear information
                                ctr.Value = DBNull.Value.ToString();
                                query += "[" + ctr.FieldName + "]=@" + ctr.FieldName.Replace(" ", "") + ",";
                                SqlParameters.Add(new SqlParameter("@" + ctr.FieldName.Replace(" ", ""), DBNull.Value));
                            }

                        }

                        if (ctr.ControlType.ToLower() == ControlType.Dynamic_DropDown.ToLower() && ctr.EnableUpdate == true)
                        {
                            if (string.IsNullOrEmpty(ctr.Value) == false)
                            {
                                //var list = await getLookupConnection(await getComponent(ctr.DataSource, AppId, ConnectionList), UserSession, ConnectionList, "", "");

                                //if (list.Where(item => item.Value == ctr.Value).ToList().Count == 0)
                                //{
                                //    resp.ResponseCode = "-1";
                                //    resp.ResponseDescription = "Invalid option selected for " + ctr.Title;
                                //    return resp;
                                //}

                                query += "[" + ctr.FieldName + "]=@" + ctr.FieldName.Replace(" ", "") + ",";
                                SqlParameters.Add(new SqlParameter("@" + ctr.FieldName.Replace(" ", ""), Encrypt_Hash(ctr.DataForm, CastDataType(ctr.DataType, ctr.Value))));
                            }
                            else
                            {
                                //clear information
                                ctr.Value = DBNull.Value.ToString();
                                query += "[" + ctr.FieldName + "]=@" + ctr.FieldName.Replace(" ", "") + ",";
                                SqlParameters.Add(new SqlParameter("@" + ctr.FieldName.Replace(" ", ""), DBNull.Value));
                            }

                        }

                        if (ctr.ControlType.ToLower() == ControlType.Dynamic_ImageLookup.ToLower() && ctr.EnableUpdate == true)
                        {
                            if (string.IsNullOrEmpty(ctr.Value) == false)
                            {
                                var list = await getLookupConnection(await getComponent(Application, ctr.DataSource), UserSession, "", "", "");

                                if (list.Where(item => item.Value == ctr.Value).ToList().Count == 0)
                                {
                                    resp.ResponseCode = "-1";
                                    resp.ResponseDescription = "Invalid option selected for " + ctr.Title;
                                    return resp;
                                }

                                query += "[" + ctr.FieldName + "]=@" + ctr.FieldName.Replace(" ", "") + ",";
                                SqlParameters.Add(new SqlParameter("@" + ctr.FieldName.Replace(" ", ""), Encrypt_Hash(ctr.DataForm, CastDataType(ctr.DataType, ctr.Value))));
                            }
                            else
                            {
                                //clear information
                                ctr.Value = DBNull.Value.ToString();
                                query += "[" + ctr.FieldName + "]=@" + ctr.FieldName.Replace(" ", "") + ",";
                                SqlParameters.Add(new SqlParameter("@" + ctr.FieldName.Replace(" ", ""), DBNull.Value));
                            }
                        }

                        if (ctr.ControlType.ToLower() == ControlType.Dynamic_CheckList.ToLower() && ctr.EnableUpdate == true)
                        {
                            if (string.IsNullOrEmpty(ctr.Value) == false)
                            {
                                //var list = await getLookupConnection(await getComponent(Application, ctr.DataSource), UserSession, "", "", "");
                                //string valueFieldParamValues = "";
                                //foreach (string key in ctr.Value.Split(new char[] { ',' }))
                                //{
                                //    var keyvalue = list.Where(item => item.Key == key).ToList();
                                //    if (keyvalue.Count() == 0)
                                //    {
                                //        resp.ResponseCode = "-1";
                                //        resp.ResponseDescription = "Invalid option selected for " + ctr.Title;
                                //        return resp;
                                //    }

                                //    //generate comma separated valuefields
                                //    valueFieldParamValues += keyvalue[0].Value + ",";
                                //}

                                //if (valueFieldParamValues != "")
                                //    valueFieldParamValues = valueFieldParamValues.Substring(0, valueFieldParamValues.Length - 1);

                                //query += "[" + ctr.FieldName + "]=@" + ctr.FieldName.Replace(" ", "") + ",";
                                //SqlParameters.Add(new SqlParameter("@" + ctr.FieldName.Replace(" ", ""), Encrypt_Hash(ctr.DataForm, CastDataType(ctr.DataType, valueFieldParamValues))));

                                query += "[" + ctr.FieldName + "]=@" + ctr.FieldName.Replace(" ", "") + ",";
                                SqlParameters.Add(new SqlParameter("@" + ctr.FieldName.Replace(" ", ""), Encrypt_Hash(ctr.DataForm, CastDataType(ctr.DataType, ctr.Value))));
                            }
                            else
                            {
                                //clear information
                                ctr.Value = DBNull.Value.ToString();
                                query += "[" + ctr.FieldName + "]=@" + ctr.FieldName.Replace(" ", "") + ",";
                                SqlParameters.Add(new SqlParameter("@" + ctr.FieldName.Replace(" ", ""), DBNull.Value));
                            }
                        }
                    }
                }

                query = query.Substring(0, query.Length - 1) + " where [" + Component.UniqueColumn.ColumnName + "]=@" + Component.UniqueColumn.ColumnName + " select * from  " + Component.DataSourceTable + " where " + Component.UniqueColumn.ColumnName + " = @" + Component.UniqueColumn.ColumnName;
            }




            //generate Array of Parameters                           
            SqlParameter[] parameters = new SqlParameter[SqlParameters.Count + 1];
            foreach (SqlParameter param in SqlParameters)
            {
                parameters[Index] = param;
                Index += 1;
            }



            //ensure all compulsory fields are populated
            foreach (var formctr in Component.Controls)
            {
                try
                {
                    if (formctr.Compulsory && Component.UniqueColumn.ColumnName != formctr.FieldName)
                    {
                        foreach (var param in parameters)
                        {
                            try
                            {
                                if (param != null)
                                {
                                    if (("@" + formctr.FieldName.ToLower().Replace(" ", "")) == param.ParameterName.ToLower().Replace(" ", "") && param.Value.ToString() == "")
                                    {
                                        resp.ResponseCode = "-1";
                                        resp.ResponseDescription = formctr.Title + " is required!";
                                        return resp;
                                    }
                                }
                            }
                            catch { }//try catch in case of null sql parameter 
                        }
                    }
                }
                catch { }//try catch in case of banner,break and other control with empty compulsory field
            }



            //determine if record is to be inserted or updated
            if (Component.DataSourceType.ToLower() == "query")
            {
                if (string.IsNullOrEmpty(PrimryColumnValue))
                {
                    if (Component.CustomInsertQuery.ToLower().Contains("--executequery"))
                    {
                        query = Component.CustomInsertQuery.Replace("--executequery", "   " + query + "   ");
                    }
                    else
                    {
                        query = Component.CustomInsertQuery;
                    }
                }
                else
                {
                    if (Component.CustomUpdateQuery.ToLower().Contains("--executequery"))
                    {
                        query = Component.CustomUpdateQuery.Replace("--executequery", "   " + query + "   ");
                    }
                    else
                    {
                        query = Component.CustomUpdateQuery;
                    }
                }
            }


            //foreach (var param in parameters)
            //{
            //    try
            //    {
            //        resp.ResponseDescription += " " + param.ParameterName + ":" + param.Value.ToString();
            //    }
            //    catch { }
            //}
            //resp.ResponseCode = "-1";
            //resp.ResponseDescription = query+ " "+ resp.ResponseDescription;
            //return resp;

            //save record
            RequestInformationTable = (await ExecuteQuery(Component, query, UserSession, parameters)).Tables[0];

            //if datasourcetype is query, there will be no uniquecolmn
            if (string.IsNullOrEmpty(Component.UniqueColumn.ColumnName) == false)
                resp.Result = "{\"Id\":\"" + Component.UniqueColumn.ColumnName + "\", \"Value\":\"" + RequestInformationTable.Rows[0][Component.UniqueColumn.ColumnName].ToString() + "\"}";


            resp.ResponseCode = "00";
            resp.ResponseDescription = "Record Saved.";
            return resp;
        }

        internal static async Task<GenericResponse> QueryApi(Application Application, string ComponentName, UserSession UserSession, string Dform, string Params)
        {
            System.Threading.Thread.CurrentThread.CurrentCulture = new System.Globalization.CultureInfo("en-GB");

            GenericResponse resp = new GenericResponse();
            SqlParameter[] SqlParams = null;
            ArrayList sqlParameterList = new ArrayList();

            var Component = await getComponent(Application, ComponentName);


            if (Component == null)
            {
                resp.ResponseCode = "404";
                resp.ResponseDescription = "Componen [" + ComponentName + "]  not found";
                return resp;
            }

            if (Component.Type.ToLower() != "query")
            {
                resp.ResponseCode = ErrorCode.WrongEndpointCall;
                resp.ResponseDescription = "Wrong method endpoint call for Component: [" + ComponentName + "]";
                return resp;
            }


            if (Component.Type.ToLower() == "query")
            {
                string Query = Component.Query;

                dynamic Parameters = Newtonsoft.Json.JsonConvert.DeserializeObject<dynamic>(Params);

                if (string.IsNullOrEmpty(Params) == false)
                {
                    var ObjParams = Newtonsoft.Json.Linq.JToken.Parse(Params);

                    Dictionary<string, string> dictObj = ObjParams.ToObject<Dictionary<string, string>>();
                    int index = 0;
                    foreach (var p in dictObj.Keys.ToArray())
                    {
                        if (dictObj[p] != null)
                        {
                            string val = dictObj[p].ToString();

                            if (Query.ToLower().Contains("qpencrypt(@" + p.ToLower() + ")"))
                            {
                                val = EncryptText(val);
                                Query = ReplaceText(Query, "qpencrypt(@" + p.ToLower() + ")", "@" + p);
                            }

                            if (Query.ToLower().Contains("qpdecrypt(@" + p.ToLower() + ")"))
                            {
                                val = DecryptText(val);
                                Query = ReplaceText(Query, "qpdecrypt(@" + p.ToLower() + ")", "@" + p);
                            }

                            if (Query.ToLower().Contains("qphash(@" + p.ToLower() + ")"))
                            {
                                val = HASH256(val);
                                Query = ReplaceText(Query, "qphash(@" + p.ToLower() + ")", "@" + p);
                            }

                            sqlParameterList.Add(new SqlParameter("@" + p, val));
                        }
                        else
                            sqlParameterList.Add(new SqlParameter("@" + p, DBNull.Value));

                        index += 1;
                    }
                }

                SqlParams = new SqlParameter[sqlParameterList.Count];
                for (int i = 0; i < sqlParameterList.Count; ++i)
                {
                    SqlParams[i] = (SqlParameter)sqlParameterList[i];
                }

                DataSet ds = await ExecuteQuery(Component, Query, UserSession, SqlParams);

                if (ds.Tables.Count == 1)
                    resp.Result = Newtonsoft.Json.JsonConvert.SerializeObject(ds.Tables[0]);
                else
                    resp.Result = Newtonsoft.Json.JsonConvert.SerializeObject(ds);

                resp.ResponseCode = "00";
                return resp;
            }

            return resp;
        }

        internal static async Task<GenericResponse> WebApi(Application Application, string url, QuickProcess.Model.api_Model request, string ComponentName, UserSession UserSession, string Dform, string Params)
        {
            var resp = new GenericResponse();

            var Component = await getComponent(Application, ComponentName);

            if (Component == null)
            {
                resp.ResponseCode = ErrorCode.Component_Not_Found;
                resp.ResponseDescription = "Component [" + ComponentName + "] not found";
                return resp;
            }

            if (Component.Type.ToLower() != "api")
            {
                resp.ResponseCode = ErrorCode.WrongEndpointCall;
                resp.ResponseDescription = "Wrong method endpoint call for Component: [" + ComponentName + "]";
                return resp;
            }

            resp.ResponseCode = "00";
            resp.Result = (JsonConvert.DeserializeObject<GenericResponse>(await postUrl(url, Component.ApiUrl, request))).Result;
            return resp;
        }

        internal static async Task<GenericResponse> getDropDownList(Application Application, string url, QuickProcess.Model.getDropDownList_Model request, UserSession UserSession, string SearchText, string SeachValue, string filterParameter)
        {
            System.Threading.Thread.CurrentThread.CurrentCulture = new System.Globalization.CultureInfo("en-GB");

            GenericResponse resp = new GenericResponse();
            var Component = await getComponent(Application, request.ComponentName.ToString());

            if (Component == null)
            {
                resp.ResponseCode = ErrorCode.Component_Not_Found;
                resp.ResponseDescription = "Component \"" + request.ComponentName.ToString() + "\" not found";
                return resp;
            }

            if (Component.Type != ComponentType.DropDownList)
            {
                resp.ResponseCode = ErrorCode.Wrong_Binding;
                resp.ResponseDescription = " Component: \"" + request.ComponentName.ToString() + "\" is not a dropdown component, hence can not be set as datasource for an input element.";
                return resp;
            }

            if (Component.FetchMethod == "api" && string.IsNullOrEmpty(url) == false)
            {
                var postResponse = JsonConvert.DeserializeObject<GenericResponse>(await postUrl(url, Component.FetchUrl, request));
                resp.ResponseCode = postResponse.ResponseCode;
                resp.Result = postResponse.Result;
                resp.ResponseDescription = postResponse.ResponseDescription;
                return resp;
            }


            resp.ResponseCode = "00";
            resp.Result = Newtonsoft.Json.JsonConvert.SerializeObject(await getLookupConnection(Component, UserSession, SearchText, SeachValue, filterParameter));

            return resp;
        }

        internal static async Task<GenericResponse> FetchComponent(Application Application, string ComponentName, string urlPath)
        {
            System.Threading.Thread.CurrentThread.CurrentCulture = new System.Globalization.CultureInfo("en-GB");

            GenericResponse resp = new GenericResponse();
            var Component = await getComponent(Application, ComponentName);


            if (Component == null)
            {
                resp.ResponseCode = "404";
                resp.ResponseDescription = "Component \"" + ComponentName + "\" not found";
                return resp;
            }


            try
            {
                if (!string.IsNullOrEmpty(Component.RecordTemplate))
                {
                    Component.RecordTemplate = "<div class=\"qpRecordTemplate\" tableId=\"" + Component.Name + "\"> " + Component.RecordTemplate + "</div>";
                }
            }
            catch { }

            try
            {
                Component.Markup = Component.Markup.Replace("~/", urlPath + "");
                Component.WrapperMarkup = Component.WrapperMarkup.Replace("~/", urlPath + "");
                Component.RecordTemplate = Component.RecordTemplate.Replace("~/", urlPath + "");
                Component.RecordMenuTemplate = Component.RecordMenuTemplate.Replace("~/", urlPath + "");
                Component.EmptyRecordTemplate = Component.EmptyRecordTemplate.Replace("~/", urlPath + "");
            }
            catch { }


            Component.Connection = null;
            Component.ConnectionString = "";
            Component.Query = "";
            Component.CustomDeleteQuery = "";
            Component.CustomInsertQuery = "";
            Component.CustomSelectQuery = "";
            Component.CustomUpdateQuery = "";
            Component.Designer_QueryColumns = "";
            Component.QueryParameters = "";


            if (Component.Type.ToLower() == "form")
                Component.Markup = Component.WrapperMarkup;

            resp.ResponseCode = "00";
            resp.Result = System.Text.Json.JsonSerializer.Serialize(Component);
            return resp;
        }



        internal static async Task<Tuple<DataTable, int>> generateSearchQuery(Component Component, string ColumnsToSelect, string FromPart, UserSession UserSession, SqlParameter[] parameters, int pageSize, int startIndex, int endIndex, string SortOrder, string CustomSearchQuery, bool download)
        {
            System.Threading.Thread.CurrentThread.CurrentCulture = new System.Globalization.CultureInfo("en-GB");

            string OrderBy = "";
            string paging = "";
            int totalRecord = 0;
            string Query = "";

            switch (Component.Connection.Engine.ToLower())
            {
                case "ms sqlserver":

                    if (string.IsNullOrEmpty(SortOrder) == false && Component.EnableSorting)
                    {
                        OrderBy += " order by ";
                        var sortOrder = System.Text.Json.JsonSerializer.Deserialize<SortOrder[]>(SortOrder);
                        bool trim = false;
                        foreach (var item in sortOrder)
                        {
                            if (item.Order.ToLower() != "none")
                            {
                                if (Component.DataSourceType == DataSourceType.Table || Component.DataSourceType == DataSourceType.View)
                                    OrderBy += Component.DataSourceTable + ".[" + Component.Columns[item.ColumnIndex].ColumnName + "] " + item.Order + ", ";
                                else
                                    OrderBy += "[" + Component.Columns[item.ColumnIndex].ColumnName + "] " + item.Order + ", ";

                                trim = true;
                            }
                        }

                        if (trim)
                            OrderBy = OrderBy.Substring(0, OrderBy.Length - 2);
                        else
                            OrderBy += Component.UniqueColumn.ColumnName;
                    }
                    else
                    {
                        if (Component.DataSourceType == DataSourceType.Table || Component.DataSourceType == DataSourceType.View)
                            OrderBy += " order by " + Component.DataSourceTable + "." + Component.UniqueColumn.ColumnName + " OFFSET   " + startIndex + " ROWS FETCH NEXT " + endIndex + " ROWS ONLY; ";
                        else
                            OrderBy += " order by  " + Component.UniqueColumn.ColumnName + " OFFSET   " + startIndex + " ROWS FETCH NEXT " + endIndex + " ROWS ONLY; ";

                    }

                    break;


            }


            string countQuery = "";
            if (Component.DataSourceType == DataSourceType.Query || Component.DataSourceType == DataSourceType.Function)
            {
                countQuery = "SELECT * INTO #Temp FROM (" + CustomSearchQuery + ") as tmpTable ";
                countQuery += " Select count(" + Component.UniqueColumn.ColumnName + ")  from #Temp  " + FromPart;
                totalRecord = int.Parse(((DataSet)await ExecuteQuery(Component, countQuery, UserSession, parameters)).Tables[0].Rows[0][0].ToString());

                if (Component.EnablePagination)
                    paging += " OFFSET   " + startIndex + " ROWS FETCH NEXT " + endIndex + " ROWS ONLY; ";

                Query = "SELECT * INTO #Temp FROM (" + CustomSearchQuery + ") as tmpTable  Select * from  #Temp   " + FromPart + OrderBy + paging + "  drop table #Temp ";
                var records = (await ExecuteQuery(Component, Query, UserSession, parameters)).Tables[0];

                //add unbound columns to datatable
                foreach (var col in Component.Columns)
                {
                    if (col.IsBound == false)
                    {
                        records.Columns.Add(col.ColumnName);
                    }
                }

                return new Tuple<DataTable, int>(records, totalRecord);
            }
            else
            {
                if (Component.DataSourceType == DataSourceType.Procedure)
                {
                    //totalRecord = int.Parse(((DataTable)await ExecuteQuery(Component, CustomSearchQuery, UserSession, parameters)).Rows.Count.ToString());
                    var records = (await ExecuteQuery(Component, CustomSearchQuery, UserSession, parameters)).Tables[0];
                    string filter = "";
                    string searchText = "";

                    //add unbound columns to datatable
                    foreach (var col in Component.Columns)
                    {
                        if (col.IsBound == false)
                        {
                            records.Columns.Add(col.ColumnName);
                        }
                    }


                    try
                    {
                        searchText = parameters.Where(param => param.ParameterName == "@Const_SearchText").FirstOrDefault().Value.ToString();
                    }
                    catch { }


                    //filter records by search criteria
                    if (records.Rows.Count > 0)
                    {
                        if (Component.Type.ToLower() == "table")
                        {
                            foreach (var col in Component.Columns)
                            {
                                if (col.Searchable)
                                {
                                    switch (string.IsNullOrEmpty(col.DataType) ? "string" : col.DataType.ToLower())
                                    {
                                        case "int":
                                        case "int32":
                                        case "int64":
                                            filter += col.ColumnName + "='" + searchText + "' or ";
                                            break;

                                        case "decimal":
                                            filter += col.ColumnName + "='" + searchText + "' or ";
                                            break;

                                        case "float":
                                            filter += col.ColumnName + "='" + searchText + "' or ";
                                            break;

                                        case "double":
                                            filter += col.ColumnName + "='" + searchText + "' or ";
                                            break;

                                        case "string":
                                        case "guid":
                                            filter += col.ColumnName + " like '%" + searchText + "%' or ";
                                            break;

                                        case "boolean":
                                            filter += col.ColumnName + "='" + searchText + "' or ";
                                            break;

                                        case "datetime":
                                            filter += col.ColumnName + "='" + searchText + "' or ";
                                            break;

                                        case "timestamp":
                                            filter += col.ColumnName + "='" + searchText + "' or ";
                                            break;

                                        default:
                                            filter += col.ColumnName + "='" + searchText + "' or ";
                                            break;
                                    }
                                }
                            }

                            if (filter != "")
                                filter = filter.Substring(0, filter.Length - 4);
                        }


                        try
                        {
                            if (Component.EnableSorting)
                            {
                                var sortOrder = System.Text.Json.JsonSerializer.Deserialize<SortOrder[]>(SortOrder);
                                if (sortOrder != null)
                                {
                                    bool trim = false;
                                    string sorting = "";
                                    foreach (var item in sortOrder)
                                    {
                                        if (item.Order.ToLower() != "none")
                                        {
                                            sorting += Component.Columns[item.ColumnIndex].ColumnName + " " + item.Order + ",";
                                            trim = true;
                                        }
                                    }

                                    if (trim)
                                        sorting = sorting.Substring(0, sorting.Length - 1);

                                    records = records.Select(filter, sorting).CopyToDataTable();
                                }
                            }
                            else
                            {
                                records = records.Select(filter).CopyToDataTable();
                            }
                        }
                        catch
                        {
                            //error is usually thrown when filter finds no record, hence clear table
                            records.Rows.Clear();
                        }
                    }



                    totalRecord = records.Rows.Count;
                    var finalRecords = records.Clone();
                    endIndex = startIndex + pageSize;

                    if (endIndex > records.Rows.Count - 1)
                        endIndex = records.Rows.Count;


                    //get records for current view page
                    if (Component.EnablePagination)
                    {
                        if (records.Rows.Count > 0)
                        {
                            try
                            {
                                for (int i = startIndex; i < endIndex; ++i)
                                {
                                    finalRecords.ImportRow(records.Rows[i]);
                                }
                            }
                            catch { }
                        }
                    }
                    else
                    {
                        finalRecords = records;
                    }

                    return new Tuple<DataTable, int>(finalRecords, totalRecord);
                }
                else
                {
                    countQuery = "Select count(" + Component.UniqueColumn.ColumnName + ")  from " + Component.DataSourceTable + "  " + FromPart;
                    totalRecord = int.Parse(((DataSet)await ExecuteQuery(Component, countQuery, UserSession, parameters)).Tables[0].Rows[0][0].ToString());

                    if (Component.EnablePagination)
                        paging += " OFFSET   " + startIndex + " ROWS FETCH NEXT " + endIndex + " ROWS ONLY; ";

                    Query = "Select " + ColumnsToSelect + " from " + Component.DataSourceTable + "  " + FromPart + OrderBy + paging;
                    var records = (await ExecuteQuery(Component, Query, UserSession, parameters)).Tables[0];

                    //add unbound columns to datatable
                    foreach (var col in Component.Columns)
                    {
                        if (col.IsBound == false)
                        {
                            records.Columns.Add(col.ColumnName);
                        }
                    }

                    return new Tuple<DataTable, int>(records, totalRecord);
                }
            }


        }

        internal static PagerSetup generatePager(DataTable records, string PageIndex, string PageSize, int rowCount)
        {
            //generate pagerSetup
            PagerSetup PagerSetup = new PagerSetup();

            int numberOfPages = 0;
            try
            {
                numberOfPages = (records == null) ? 0 : rowCount / int.Parse(PageSize);
                numberOfPages += (((records == null) ? 0 : rowCount % int.Parse(PageSize)) > 0) ? 1 : 0;
            }
            catch { }


            PagerSetup.InViewPageIndices = new string[numberOfPages];
            for (int i = 0; i < numberOfPages; ++i)
            {
                PagerSetup.InViewPageIndices[i] = i.ToString();
            }


            PagerSetup.TotalRecord = (records == null) ? 0 : rowCount;
            PagerSetup.ActiveIndex = int.Parse(PageIndex);
            PagerSetup.TotalPages = numberOfPages;
            return PagerSetup;
        }

        internal static string ReplaceText(string text, string oldVal, string newVal)
        {
            string pattern = "";
            foreach (char chr in oldVal)
            {
                pattern += "[" + chr.ToString().ToUpper() + chr.ToString().ToLower() + "]";
            }
            return System.Text.RegularExpressions.Regex.Replace(text, "(" + pattern + ")", newVal);
        }

        private static int getFormUniqueColumnIndex(Component Component)
        {
            int index = 0;
            foreach (FormControls ctr in Component.Controls)
            {
                if (ctr.FieldName.ToLower() == Component.UniqueColumn.ColumnName.ToLower())
                {
                    return index;
                }
                index += 1;
            }

            return -1;
        }


        private static async Task<List<SqlParameter>> generateFormParameters(Application Application, UserSession UserSession, Component Component, int PrimaryColumnIndex, string Dform)
        {
            List<SqlParameter> SqlParameters = new List<SqlParameter>();

            foreach (FormControls ctr in Component.Controls)
            {
                if ((ctr.ControlType.ToLower() == ControlType.Text.ToLower() || ctr.ControlType.ToLower() == ControlType.Rating.ToLower() || ctr.ControlType.ToLower() == ControlType.ToggleList.ToLower() || ctr.ControlType.ToLower() == ControlType.Toggle.ToLower() || ctr.ControlType.ToLower() == ControlType.Dynamic_CheckList.ToLower() || ctr.ControlType.ToLower() == ControlType.Static_CheckList.ToLower() || ctr.ControlType.ToLower() == ControlType.RadioButtonList.ToLower() || ctr.ControlType.ToLower() == ControlType.SignPad.ToLower() || ctr.ControlType.ToLower() == ControlType.User_Phone_Validator.ToLower() || ctr.ControlType.ToLower() == ControlType.User_Email_Validator.ToLower() || ctr.ControlType.ToLower() == ControlType.IPAddress.ToLower() || ctr.ControlType.ToLower() == ControlType.Geolocation.ToLower() || ctr.ControlType.ToLower() == ControlType.HtmlEditor.ToLower() || ctr.ControlType.ToLower() == ControlType.ServerField.ToLower() || ctr.ControlType.ToLower() == ControlType.Phone.ToLower() || ctr.ControlType.ToLower() == ControlType.Month.ToLower() || ctr.ControlType.ToLower() == ControlType.Week.ToLower() || ctr.ControlType.ToLower() == ControlType.ColourPicker.ToLower() || ctr.ControlType.ToLower() == ControlType.Range.ToLower() || ctr.ControlType.ToLower() == ControlType.LABEL.ToLower() || ctr.ControlType.ToLower() == ControlType.HIDDEN_FIELD.ToLower() || ctr.ControlType.ToLower() == ControlType.Static_DropDown.ToLower() || ctr.ControlType.ToLower() == ControlType.Static_ImageLookup.ToLower() || ctr.ControlType.ToLower() == ControlType.Text_Area.ToLower() || ctr.ControlType.ToLower() == ControlType.StaffLookUp.ToLower() || ctr.ControlType.ToLower() == ControlType.FILEUPLOAD.ToLower() || ctr.ControlType.ToLower() == ControlType.EMAIL.ToLower()))
                {
                    if (string.IsNullOrEmpty(ctr.Value))
                    {
                        SqlParameters.Add(new SqlParameter("@" + ctr.FieldName.Replace(" ", ""), DBNull.Value));
                    }
                    else
                    {
                        SqlParameters.Add(new SqlParameter("@" + ctr.FieldName.Replace(" ", ""), Encrypt_Hash(ctr.DataForm, CastDataType(ctr.DataType, ctr.Value))));
                    }
                }

                if ((ctr.ControlType.ToLower() == ControlType.CheckBox.ToLower()))
                {
                    if (string.IsNullOrEmpty(ctr.Value))
                    {
                        SqlParameters.Add(new SqlParameter("@" + ctr.FieldName.Replace(" ", ""), false));
                    }
                    else
                    {
                        SqlParameters.Add(new SqlParameter("@" + ctr.FieldName.Replace(" ", ""), bool.Parse(ctr.Value)));
                    }
                }

                if (ctr.ControlType.ToLower() == ControlType.GUID.ToLower())
                {
                    SqlParameters.Add(new SqlParameter("@" + ctr.FieldName.Replace(" ", ""), Encrypt_Hash(ctr.DataForm, Guid.NewGuid().ToString())));
                }

                if ((ctr.ControlType.ToLower() == ControlType.Number.ToLower() || ctr.ControlType.ToLower() == ControlType.Number_CommaSeparated.ToLower() || ctr.ControlType.ToLower() == ControlType.Money.ToLower()))
                {
                    if (string.IsNullOrEmpty(ctr.Value))
                    {
                        SqlParameters.Add(new SqlParameter("@" + ctr.FieldName.Replace(" ", ""), DBNull.Value));
                    }
                    else
                    {
                        SqlParameters.Add(new SqlParameter("@" + ctr.FieldName.Replace(" ", ""), Encrypt_Hash(ctr.DataForm, CastDataType(ctr.DataType, ctr.Value.Replace(",", "")))));
                    }
                }

                if ((ctr.ControlType.ToLower() == ControlType.PASSWORD.ToLower() || ctr.ControlType.ToLower() == ControlType.User_Password_Validator.ToLower() || ctr.ControlType.ToLower() == ControlType.User_PIN_Validator.ToLower()))
                {
                    if (string.IsNullOrEmpty(ctr.Value))
                    {
                        SqlParameters.Add(new SqlParameter("@" + ctr.FieldName.Replace(" ", ""), DBNull.Value));
                    }
                    else
                    {
                        SqlParameters.Add(new SqlParameter("@" + ctr.FieldName.Replace(" ", ""), Encrypt_Hash(ctr.DataForm, ctr.Value)));
                    }
                }

                if (ctr.ControlType.ToLower() == ControlType.CONST_USERNAME.ToLower())
                {
                    SqlParameters.Add(new SqlParameter("@" + ctr.FieldName.Replace(" ", ""), Encrypt_Hash(ctr.DataForm, UserSession.UserName)));
                }

                if (ctr.ControlType.ToLower() == ControlType.CONST_TENANTID.ToLower())
                {
                    SqlParameters.Add(new SqlParameter("@" + ctr.FieldName.Replace(" ", ""), Encrypt_Hash(ctr.DataForm, UserSession.TenantID)));
                }

                if (ctr.ControlType.ToLower() == ControlType.CONST_SESSIONID.ToLower())
                {
                    SqlParameters.Add(new SqlParameter("@" + ctr.FieldName.Replace(" ", ""), Encrypt_Hash(ctr.DataForm, UserSession.SessionID)));
                }

                if (ctr.ControlType.ToLower() == ControlType.CurrentDateTime.ToLower())
                {
                    SqlParameters.Add(new SqlParameter("@" + ctr.FieldName.Replace(" ", ""), Encrypt_Hash(ctr.DataForm, DateTime.Now)));
                }

                if ((ctr.ControlType.ToLower() == ControlType.Date.ToLower() || ctr.ControlType.ToLower() == ControlType.DateTime.ToLower()))
                {
                    if (string.IsNullOrEmpty(ctr.Value))
                    {
                        SqlParameters.Add(new SqlParameter("@" + ctr.FieldName.Replace(" ", ""), DBNull.Value));
                    }
                    else
                    {
                        if (Dform == "mdy")
                        {
                            string date = ctr.Value.Split(new char[] { '/' })[1] + "/" + ctr.Value.Split(new char[] { '/' })[0] + "/" + ctr.Value.Split(new char[] { '/' })[2];
                            SqlParameters.Add(new SqlParameter("@" + ctr.FieldName.Replace(" ", ""), Encrypt_Hash(ctr.DataForm, DateTime.Parse(date))));
                        }
                        else
                        {
                            SqlParameters.Add(new SqlParameter("@" + ctr.FieldName.Replace(" ", ""), Encrypt_Hash(ctr.DataForm, DateTime.Parse(ctr.Value))));
                        }
                    }
                }

                if (string.IsNullOrEmpty(ctr.DataSource) == false)
                {
                    if (ctr.ControlType.ToLower() == ControlType.AutoComplete.ToLower())
                    {
                        if (string.IsNullOrEmpty(ctr.Value))
                        {
                            SqlParameters.Add(new SqlParameter("@" + ctr.FieldName.Replace(" ", ""), DBNull.Value));
                        }
                        else
                        {
                            var list = await getLookupConnection(await getComponent(Application, ctr.DataSource), UserSession, "", "", "");
                            var keyvalue = list.Where(item => item.Key == ctr.Value).ToList();
                            if (keyvalue.Count() == 0)
                            {
                                throw new Exception("Invalid option selected for " + ctr.Title);
                            }

                            SqlParameters.Add(new SqlParameter("@" + ctr.FieldName.Replace(" ", ""), Encrypt_Hash(ctr.DataForm, CastDataType(ctr.DataType, keyvalue[0].Value))));
                        }
                    }

                    if (ctr.ControlType.ToLower() == ControlType.Dynamic_DropDown.ToLower())
                    {
                        if (string.IsNullOrEmpty(ctr.Value))
                        {
                            SqlParameters.Add(new SqlParameter("@" + ctr.FieldName.Replace(" ", ""), DBNull.Value));
                        }
                        else
                        {
                            //var list = await getLookupConnection(await getComponent(ctr.DataSource, AppId, ConnectionList), UserSession, ConnectionList, "", "");

                            //if (list.Where(item => item.Value == ctr.Value).ToList().Count == 0)
                            //{
                            //    resp.ResponseCode = "-1";
                            //    resp.ResponseDescription = "Invalid option selected for " + ctr.Title;
                            //    return resp;
                            //} 

                            SqlParameters.Add(new SqlParameter("@" + ctr.FieldName.Replace(" ", ""), Encrypt_Hash(ctr.DataForm, CastDataType(ctr.DataType, ctr.Value))));
                        }
                    }

                    if (ctr.ControlType.ToLower() == ControlType.Dynamic_ImageLookup.ToLower())
                    {
                        if (string.IsNullOrEmpty(ctr.Value))
                        {
                            SqlParameters.Add(new SqlParameter("@" + ctr.FieldName.Replace(" ", ""), DBNull.Value));
                        }
                        else
                        {
                            var list = await getLookupConnection(await getComponent(Application, ctr.DataSource), UserSession, "", "", "");

                            if (list.Where(item => item.Value == ctr.Value).ToList().Count == 0)
                            {
                                throw new Exception("Invalid option selected for " + ctr.Title);
                            }

                            SqlParameters.Add(new SqlParameter("@" + ctr.FieldName.Replace(" ", ""), Encrypt_Hash(ctr.DataForm, CastDataType(ctr.DataType, ctr.Value))));
                        }
                    }

                    if (ctr.ControlType.ToLower() == ControlType.TABLE_LOOKUP.ToLower())
                    {
                        if (string.IsNullOrEmpty(ctr.Value))
                        {
                            SqlParameters.Add(new SqlParameter("@" + ctr.FieldName.Replace(" ", ""), DBNull.Value));
                        }
                        else
                        {
                            var list = await getLookupConnection(await getComponent(Application, ctr.DataSource), UserSession, "", "", "");
                            var keyvalue = list.Where(item => item.Key == ctr.Value).ToList();
                            if (keyvalue.Count() == 0)
                            {
                                throw new Exception("Invalid option selected for " + ctr.Title);
                            }
                            SqlParameters.Add(new SqlParameter("@" + ctr.FieldName.Replace(" ", ""), Encrypt_Hash(ctr.DataForm, CastDataType(ctr.DataType, keyvalue[0].Value))));
                        }
                    }

                    if (ctr.ControlType.ToLower() == ControlType.Dynamic_CheckList.ToLower())
                    {
                        if (string.IsNullOrEmpty(ctr.Value))
                        {
                            SqlParameters.Add(new SqlParameter("@" + ctr.FieldName.Replace(" ", ""), DBNull.Value));
                        }
                        else
                        {
                            //get datasource for control in datasource collection of current process
                            var list = await getLookupConnection(await getComponent(Application, ctr.DataSource), UserSession, "", "", "");
                            string valueFieldParamValues = "";
                            foreach (string key in ctr.Value.Split(new char[] { ',' }))
                            {
                                var keyvalue = list.Where(item => item.Key == key).ToList();
                                if (keyvalue.Count() == 0)
                                {
                                    throw new Exception("Invalid option selected for " + ctr.Title);
                                }

                                //generate comma separated valuefields
                                valueFieldParamValues += keyvalue[0].Value + ",";
                            }

                            if (valueFieldParamValues != "")
                                valueFieldParamValues = valueFieldParamValues.Substring(0, valueFieldParamValues.Length - 1);

                            SqlParameters.Add(new SqlParameter("@" + ctr.FieldName.Replace(" ", ""), Encrypt_Hash(ctr.DataForm, CastDataType(ctr.DataType, valueFieldParamValues))));
                        }
                    }
                }

            }

            return SqlParameters;
        }

        private static Tuple<string, string> generateFormQuery(Application Application, Component Component, int PrimaryColumnIndex, bool isNew, string Dform)
        {
            string query = "";
            string values = "";

            if (isNew)
            {
                foreach (FormControls ctr in Component.Controls)
                {
                    //skip control if its identity or readonly or insert is disabled for control
                    if (PrimaryColumnIndex != -1)
                    {
                        if (ctr.FieldName.ToLower() == Component.Controls[PrimaryColumnIndex].FieldName.ToLower() || ctr.ReadOnly || ctr.EnableInsert == false || ctr.EnableSave == false)
                        {
                            continue;
                        }
                    }

                    if ((ctr.ControlType.ToLower() == ControlType.Text.ToLower() || ctr.ControlType.ToLower() == ControlType.Rating.ToLower() || ctr.ControlType.ToLower() == ControlType.ToggleList.ToLower() || ctr.ControlType.ToLower() == ControlType.Toggle.ToLower() || ctr.ControlType.ToLower() == ControlType.Dynamic_CheckList.ToLower() || ctr.ControlType.ToLower() == ControlType.Static_CheckList.ToLower() || ctr.ControlType.ToLower() == ControlType.RadioButtonList.ToLower() || ctr.ControlType.ToLower() == ControlType.SignPad.ToLower() || ctr.ControlType.ToLower() == ControlType.User_Phone_Validator.ToLower() || ctr.ControlType.ToLower() == ControlType.User_Email_Validator.ToLower() || ctr.ControlType.ToLower() == ControlType.IPAddress.ToLower() || ctr.ControlType.ToLower() == ControlType.Geolocation.ToLower() || ctr.ControlType.ToLower() == ControlType.HtmlEditor.ToLower() || ctr.ControlType.ToLower() == ControlType.ServerField.ToLower() || ctr.ControlType.ToLower() == ControlType.Phone.ToLower() || ctr.ControlType.ToLower() == ControlType.Month.ToLower() || ctr.ControlType.ToLower() == ControlType.Week.ToLower() || ctr.ControlType.ToLower() == ControlType.ColourPicker.ToLower() || ctr.ControlType.ToLower() == ControlType.Range.ToLower() || ctr.ControlType.ToLower() == ControlType.LABEL.ToLower() || ctr.ControlType.ToLower() == ControlType.HIDDEN_FIELD.ToLower() || ctr.ControlType.ToLower() == ControlType.Static_DropDown.ToLower() || ctr.ControlType.ToLower() == ControlType.Static_ImageLookup.ToLower() || ctr.ControlType.ToLower() == ControlType.Text_Area.ToLower() || ctr.ControlType.ToLower() == ControlType.StaffLookUp.ToLower() || ctr.ControlType.ToLower() == ControlType.FILEUPLOAD.ToLower() || ctr.ControlType.ToLower() == ControlType.EMAIL.ToLower()))
                    {
                        query += "[" + ctr.FieldName + "],";
                        values += "@" + ctr.FieldName.Replace(" ", "") + ",";
                    }

                    if ((ctr.ControlType.ToLower() == ControlType.CheckBox.ToLower()))
                    {
                        query += "[" + ctr.FieldName + "],";
                        values += "@" + ctr.FieldName.Replace(" ", "") + ",";
                    }

                    if (ctr.ControlType.ToLower() == ControlType.GUID.ToLower())
                    {
                        query += "[" + ctr.FieldName + "],";
                        values += "@" + ctr.FieldName.Replace(" ", "") + ",";
                    }

                    if ((ctr.ControlType.ToLower() == ControlType.Number.ToLower() || ctr.ControlType.ToLower() == ControlType.Number_CommaSeparated.ToLower() || ctr.ControlType.ToLower() == ControlType.Money.ToLower()))
                    {
                        query += "[" + ctr.FieldName + "],";
                        values += "@" + ctr.FieldName.Replace(" ", "") + ",";
                    }

                    if ((ctr.ControlType.ToLower() == ControlType.PASSWORD.ToLower() || ctr.ControlType.ToLower() == ControlType.User_Password_Validator.ToLower() || ctr.ControlType.ToLower() == ControlType.User_PIN_Validator.ToLower()))
                    {
                        query += "[" + ctr.FieldName + "],";
                        values += "@" + ctr.FieldName.Replace(" ", "") + ",";
                    }

                    if (ctr.ControlType.ToLower() == ControlType.CONST_USERNAME.ToLower())
                    {
                        query += "[" + ctr.FieldName + "],";
                        values += "@" + ctr.FieldName.Replace(" ", "") + ",";
                    }

                    if (ctr.ControlType.ToLower() == ControlType.CONST_SESSIONID.ToLower())
                    {
                        query += "[" + ctr.FieldName + "],";
                        values += "@" + ctr.FieldName.Replace(" ", "") + ",";
                    }

                    if (ctr.ControlType.ToLower() == ControlType.CONST_TENANTID.ToLower())
                    {
                        query += "[" + ctr.FieldName + "],";
                        values += "@" + ctr.FieldName.Replace(" ", "") + ",";
                    }

                    if (ctr.ControlType.ToLower() == ControlType.CurrentDateTime.ToLower())
                    {
                        query += "[" + ctr.FieldName + "],";
                        values += "@" + ctr.FieldName.Replace(" ", "") + ",";
                    }

                    if ((ctr.ControlType.ToLower() == ControlType.Date.ToLower() || ctr.ControlType.ToLower() == ControlType.DateTime.ToLower()))
                    {
                        query += "[" + ctr.FieldName + "],";
                        values += "@" + ctr.FieldName.Replace(" ", "") + ",";
                    }

                    if (string.IsNullOrEmpty(ctr.DataSource) == false)
                    {
                        if (ctr.ControlType.ToLower() == ControlType.AutoComplete.ToLower())
                        {
                            if (!string.IsNullOrEmpty(ctr.Value))
                            {
                                query += ctr.FieldName + ",";
                                values += "@" + ctr.FieldName.Replace(" ", "") + ",";
                            }
                        }

                        if (ctr.ControlType.ToLower() == ControlType.Dynamic_DropDown.ToLower())
                        {
                            if (!string.IsNullOrEmpty(ctr.Value))
                            {
                                //var list = await getLookupConnection(await getComponent(ctr.DataSource, AppId, ConnectionList), UserSession, ConnectionList, "", "");

                                //if (list.Where(item => item.Value == ctr.Value).ToList().Count == 0)
                                //{
                                //    resp.ResponseCode = "-1";
                                //    resp.ResponseDescription = "Invalid option selected for " + ctr.Title;
                                //    return resp;
                                //}

                                query += "[" + ctr.FieldName + "],";
                                values += "@" + ctr.FieldName.Replace(" ", "") + ",";
                            }
                        }

                        if (ctr.ControlType.ToLower() == ControlType.Dynamic_ImageLookup.ToLower())
                        {
                            if (!string.IsNullOrEmpty(ctr.Value))
                            {
                                query += "[" + ctr.FieldName + "],";
                                values += "@" + ctr.FieldName.Replace(" ", "") + ",";
                            }
                        }

                        if (ctr.ControlType.ToLower() == ControlType.TABLE_LOOKUP.ToLower())
                        {
                            if (!string.IsNullOrEmpty(ctr.Value))
                            {
                                query += "[" + ctr.FieldName + "],";
                                values += "@" + ctr.FieldName.Replace(" ", "") + ",";
                            }
                        }

                        if (ctr.ControlType.ToLower() == ControlType.Dynamic_CheckList.ToLower())
                        {
                            if (!string.IsNullOrEmpty(ctr.Value))
                            {
                                query += "[" + ctr.FieldName + "],";
                                values += "@" + ctr.FieldName.Replace(" ", "") + ",";
                            }
                        }
                    }
                }
            }

            if (!isNew)
            {
                foreach (FormControls ctr in Component.Controls)
                {
                    if ((ctr.ControlType.ToLower() == ControlType.Text.ToLower() || ctr.ControlType.ToLower() == ControlType.Rating.ToLower() || ctr.ControlType.ToLower() == ControlType.ToggleList.ToLower() || ctr.ControlType.ToLower() == ControlType.Toggle.ToLower() || ctr.ControlType.ToLower() == ControlType.Dynamic_CheckList.ToLower() || ctr.ControlType.ToLower() == ControlType.RadioButtonList.ToLower() || ctr.ControlType.ToLower() == ControlType.SignPad.ToLower() || ctr.ControlType.ToLower() == ControlType.User_Phone_Validator.ToLower() || ctr.ControlType.ToLower() == ControlType.User_Email_Validator.ToLower() || ctr.ControlType.ToLower() == ControlType.IPAddress.ToLower() || ctr.ControlType.ToLower() == ControlType.Geolocation.ToLower() || ctr.ControlType.ToLower() == ControlType.HtmlEditor.ToLower() || ctr.ControlType.ToLower() == ControlType.ServerField.ToLower() || ctr.ControlType.ToLower() == ControlType.Phone.ToLower() || ctr.ControlType.ToLower() == ControlType.Month.ToLower() || ctr.ControlType.ToLower() == ControlType.Week.ToLower() || ctr.ControlType.ToLower() == ControlType.ColourPicker.ToLower() || ctr.ControlType.ToLower() == ControlType.Range.ToLower() || ctr.ControlType.ToLower() == ControlType.LABEL.ToLower() || ctr.ControlType.ToLower() == ControlType.HIDDEN_FIELD.ToLower() || ctr.ControlType.ToLower() == ControlType.Static_DropDown.ToLower() || ctr.ControlType.ToLower() == ControlType.Static_ImageLookup.ToLower() || ctr.ControlType.ToLower() == ControlType.Text_Area.ToLower() || ctr.ControlType.ToLower() == ControlType.StaffLookUp.ToLower() || ctr.ControlType.ToLower() == ControlType.FILEUPLOAD.ToLower() || ctr.ControlType.ToLower() == ControlType.EMAIL.ToLower()) && ctr.EnableUpdate == true)
                    {
                        if (string.IsNullOrEmpty(ctr.Value) == false)
                        {
                            query += "[" + ctr.FieldName + "]=@" + ctr.FieldName.Replace(" ", "") + ",";
                        }
                        else
                        {
                            ctr.Value = DBNull.Value.ToString();
                            query += "[" + ctr.FieldName + "]=@" + ctr.FieldName.Replace(" ", "") + ",";
                        }
                    }

                    if ((ctr.ControlType.ToLower() == ControlType.CheckBox.ToLower()))
                    {
                        query += "[" + ctr.FieldName + "]=@" + ctr.FieldName.Replace(" ", "") + ",";
                    }

                    if (ctr.ControlType.ToLower() == ControlType.GUID.ToLower() && ctr.EnableUpdate == true)
                    {
                        query += "[" + ctr.FieldName + "]=@" + ctr.FieldName.Replace(" ", "") + ",";
                    }

                    //check if password was changed hence the masked value would have been updated otherwise simply skip as the password field was not changed
                    if ((ctr.ControlType.ToLower() == ControlType.PASSWORD.ToLower() || ctr.ControlType.ToLower() == ControlType.User_Password_Validator.ToLower() || ctr.ControlType.ToLower() == ControlType.User_PIN_Validator.ToLower()) && ctr.Value != "**********" && ctr.EnableUpdate == true)
                    {
                        if (string.IsNullOrEmpty(ctr.Value) == false)
                        {
                            query += "[" + ctr.FieldName + "]=@" + ctr.FieldName.Replace(" ", "") + ",";
                        }
                        else
                        {
                            query += "[" + ctr.FieldName + "]=@" + ctr.FieldName.Replace(" ", "") + ",";
                        }
                    }

                    if (ctr.ControlType.ToLower() == ControlType.CurrentDateTime.ToLower() && ctr.EnableUpdate == true)
                    {
                        query += "[" + ctr.FieldName + "]=@" + ctr.FieldName.Replace(" ", "") + ",";
                    }

                    if ((ctr.ControlType.ToLower() == ControlType.Number.ToLower() || ctr.ControlType.ToLower() == ControlType.Number_CommaSeparated.ToLower() || ctr.ControlType.ToLower() == ControlType.Money.ToLower()) && ctr.EnableUpdate == true)
                    {
                        if (string.IsNullOrEmpty(ctr.Value) == false)
                        {
                            query += "[" + ctr.FieldName + "]=@" + ctr.FieldName.Replace(" ", "") + ",";
                        }
                        else
                        {
                            query += "[" + ctr.FieldName + "]=@" + ctr.FieldName.Replace(" ", "") + ",";
                        }
                    }

                    if ((ctr.ControlType.ToLower() == ControlType.Date.ToLower() || ctr.ControlType.ToLower() == ControlType.DateTime.ToLower()) && string.IsNullOrEmpty(ctr.Value) == false && ctr.EnableUpdate == true)
                    {
                        if (string.IsNullOrEmpty(ctr.Value) == false)
                        {
                            query += "[" + ctr.FieldName + "]=@" + ctr.FieldName.Replace(" ", "") + ",";
                        }
                        else
                        {
                            ctr.Value = DBNull.Value.ToString();
                            query += "[" + ctr.FieldName + "]=@" + ctr.FieldName.Replace(" ", "") + ",";
                        }
                    }

                    if (ctr.ControlType.ToLower() == ControlType.CONST_USERNAME.ToLower() && ctr.EnableUpdate == true)
                    {
                        query += "[" + ctr.FieldName + "]=@" + ctr.FieldName.Replace(" ", "") + ",";
                    }

                    if (ctr.ControlType.ToLower() == ControlType.CONST_SESSIONID.ToLower() && ctr.EnableUpdate == true)
                    {
                        query += "[" + ctr.FieldName + "]=@" + ctr.FieldName.Replace(" ", "") + ",";
                    }

                    if (ctr.ControlType.ToLower() == ControlType.CONST_TENANTID.ToLower() && ctr.EnableUpdate == true)
                    {
                        query += "[" + ctr.FieldName + "]=@" + ctr.FieldName.Replace(" ", "") + ",";
                    }

                    if (string.IsNullOrEmpty(ctr.DataSource) == false)
                    {
                        if ((ctr.ControlType.ToLower() == ControlType.AutoComplete.ToLower()) && ctr.EnableUpdate == true)
                        {
                            if (string.IsNullOrEmpty(ctr.Value) == false)
                            {
                                query += "[" + ctr.FieldName + "]=@" + ctr.FieldName.Replace(" ", "") + ",";
                            }
                            else
                            {
                                //clear information
                                ctr.Value = DBNull.Value.ToString();
                                query += "[" + ctr.FieldName + "]=@" + ctr.FieldName.Replace(" ", "") + ",";
                            }

                        }

                        if (ctr.ControlType.ToLower() == ControlType.TABLE_LOOKUP.ToLower() && ctr.EnableUpdate == true)
                        {
                            if (string.IsNullOrEmpty(ctr.Value) == false)
                            {
                                query += "[" + ctr.FieldName + "]=@" + ctr.FieldName.Replace(" ", "") + ",";
                            }
                            else
                            {
                                //clear information
                                ctr.Value = DBNull.Value.ToString();
                                query += "[" + ctr.FieldName + "]=@" + ctr.FieldName.Replace(" ", "") + ",";
                            }

                        }

                        if (ctr.ControlType.ToLower() == ControlType.Dynamic_DropDown.ToLower() && ctr.EnableUpdate == true)
                        {
                            if (string.IsNullOrEmpty(ctr.Value) == false)
                            {
                                //var list = await getLookupConnection(await getComponent(ctr.DataSource, AppId, ConnectionList), UserSession, ConnectionList, "", "");

                                //if (list.Where(item => item.Value == ctr.Value).ToList().Count == 0)
                                //{
                                //    resp.ResponseCode = "-1";
                                //    resp.ResponseDescription = "Invalid option selected for " + ctr.Title;
                                //    return resp;
                                //}

                                query += "[" + ctr.FieldName + "]=@" + ctr.FieldName.Replace(" ", "") + ",";
                            }
                            else
                            {
                                //clear information
                                ctr.Value = DBNull.Value.ToString();
                                query += "[" + ctr.FieldName + "]=@" + ctr.FieldName.Replace(" ", "") + ",";
                            }

                        }

                        if (ctr.ControlType.ToLower() == ControlType.Dynamic_ImageLookup.ToLower() && ctr.EnableUpdate == true)
                        {
                            if (string.IsNullOrEmpty(ctr.Value) == false)
                            {
                                query += "[" + ctr.FieldName + "]=@" + ctr.FieldName.Replace(" ", "") + ",";
                            }
                            else
                            {
                                //clear information
                                ctr.Value = DBNull.Value.ToString();
                                query += "[" + ctr.FieldName + "]=@" + ctr.FieldName.Replace(" ", "") + ",";
                            }
                        }

                        if (ctr.ControlType.ToLower() == ControlType.Dynamic_CheckList.ToLower() && ctr.EnableUpdate == true)
                        {
                            if (string.IsNullOrEmpty(ctr.Value) == false)
                            {
                                query += "[" + ctr.FieldName + "]=@" + ctr.FieldName.Replace(" ", "") + ",";
                            }
                            else
                            {
                                query += "[" + ctr.FieldName + "]=@" + ctr.FieldName.Replace(" ", "") + ",";
                            }
                        }
                    }
                }

                query = query.Substring(0, query.Length - 1) + " where [" + Component.UniqueColumn.ColumnName + "]=@" + Component.UniqueColumn.ColumnName + " select * from  " + Component.DataSourceTable + " where " + Component.UniqueColumn.ColumnName + " = @" + Component.UniqueColumn.ColumnName;

            }

            if (isNew)
            {
                //append query to get identity/auto increment column
                switch (Component.Connection.Engine.ToLower())
                {
                    case "ms sqlserver":
                        query = query.Substring(0, query.Length - 1) + ") values(" + values.Substring(0, values.Length - 1) + ")  select * from " + Component.DataSourceTable + " where " + Component.UniqueColumn.ColumnName + "= scope_identity()";
                        break;

                    case "oracle":
                        query = query.Substring(0, query.Length - 1) + ") values(" + values.Substring(0, values.Length - 1) + "); variable Inserted_Value number; returning " + Component.UniqueColumn.ColumnName + " into :Inserted_Value; select :Inserted_Value from DUAL;   select  * from " + Component.DataSourceTable + " where " + Component.UniqueColumn.ColumnName + "= Inserted_Value; ";
                        //query = query.Substring(0, query.Length - 1) + ") values(" + values.Substring(0, values.Length - 1) + "); variable Inserted_Value number; returning " + DataSource.PrimaryColumn.ColumnName + " into :Inserted_Value; select :Inserted_Value from DUAL ";
                        break;

                    case "my sql":
                        query = query.Substring(0, query.Length - 1) + ") values(" + values.Substring(0, values.Length - 1) + ");  select  * from " + Component.DataSourceTable + " where " + Component.UniqueColumn.ColumnName + "= LAST_INSERT_ID();";
                        break;

                    case "postgre sql":
                        query = query.Substring(0, query.Length - 1) + ") values(" + values.Substring(0, values.Length - 1) + ")   " + ((Component.UniqueColumn != null) ? " RETURNING * " : "") + ";";
                        // query = query.Substring(0, query.Length - 1) + ") values(" + values.Substring(0, values.Length - 1) + ")   " + ((DataSource.PrimaryColumn != null) ? " RETURNING " + DataSource.PrimaryColumn.ColumnName : "") + ";";
                        break;
                }
            }

            return new Tuple<string, string>(query, values);
        }

        private static string GetClientIp()
        {
            return "";
        }

        internal static async Task<Component> BindDropDownList(Component Component, string AppId, UserSession UserSession, List<QuickProcess.ConnectionList> ConnectionList, string SearchText, string filterParameter)
        {
            System.Threading.Thread.CurrentThread.CurrentCulture = new System.Globalization.CultureInfo("en-GB");

            //get list for all dynamic dropdowns
            foreach (FormControls ctr in Component.Controls)
            {
                if (ctr.ControlType.ToLower() == ControlType.Dynamic_DropDown.ToLower() || ctr.ControlType.ToLower() == ControlType.Dynamic_CheckList.ToLower() || ctr.ControlType.ToLower() == ControlType.Dynamic_ImageLookup.ToLower())
                {
                    var list = await getLookupConnection(Component, UserSession, "", "", filterParameter);
                    ctr.List = list;
                }
            }

            return Component;
        }

        private static async Task<ControlList[]> getLookupConnection(Component Component, UserSession UserSession, string SearchText, string SearchValue, string filterParameter)
        {
            var List = new ControlList[0];
            List<SqlParameter> parameters = new List<SqlParameter>();
            SqlParameter[] SqlParams = null;
            ArrayList sqlParameterList = new ArrayList();
            sqlParameterList.Add(new SqlParameter("@Const_SearchValue", SearchValue));
            sqlParameterList.Add(new SqlParameter("@Const_SearchText", SearchText));

            //connect to source and pull list items
            string Query = "";

            if (Component.DataSourceType.ToLower() == "table" || Component.DataSourceType.ToLower() == "view")
                Query = "Select " + Component.ValueField + "," + Component.DisplayField + " from " + Component.DataSourceTable;
            else
            {
                Query = Component.CustomSelectQuery;
            }

            //typically for auto-complete
            if (string.IsNullOrEmpty(SearchValue) == false)
            {
                if (Component.DataSourceType.ToLower() == "table" || Component.DataSourceType.ToLower() == "view")
                    Query += " where   " + Component.ValueField + " = @Const_SearchValue   order by " + Component.DisplayField;
            }
            else
            {
                if (string.IsNullOrEmpty(SearchText) == false)
                {
                    if (Component.DataSourceType.ToLower() == "table" || Component.DataSourceType.ToLower() == "view")
                        Query += " where   " + Component.DisplayField + " like '%'+@Const_SearchText+'%'   order by " + Component.DisplayField;
                }
            }


            try
            {
                if (string.IsNullOrEmpty(filterParameter) == false)
                {
                    var ObjParams = Newtonsoft.Json.Linq.JToken.Parse(filterParameter);

                    Dictionary<string, string> dictObj = ObjParams.ToObject<Dictionary<string, string>>();

                    foreach (var p in dictObj.Keys.ToArray())
                    {
                        if (dictObj[p] != null)
                        {
                            string val = dictObj[p].ToString();

                            if (Query.ToLower().Contains("qpencrypt(@" + p.ToLower() + ")"))
                            {
                                val = EncryptText(val);
                                Query = ReplaceText(Query, "qpencrypt(@" + p.ToLower() + ")", "@" + p);
                            }

                            if (Query.ToLower().Contains("qphash(@" + p.ToLower() + ")"))
                            {
                                val = HASH256(val);
                                Query = ReplaceText(Query, "qphash(@" + p.ToLower() + ")", "@" + p);
                            }

                            sqlParameterList.Add(new SqlParameter("@" + p, val));
                        }
                        else
                            sqlParameterList.Add(new SqlParameter("@" + p, DBNull.Value));
                    }
                }
            }
            catch (Exception ex) { }


            SqlParams = new SqlParameter[sqlParameterList.Count];
            for (int i = 0; i < sqlParameterList.Count; ++i)
            {
                SqlParams[i] = (SqlParameter)sqlParameterList[i];
            }


            //pull lookup list from db
            DataTable records = (await ExecuteQuery(Component, Query, UserSession, SqlParams)).Tables[0];

            //for autocomplete, if searchvalue was sent from browser and developer did not programmatically use the @Const_SearchValue in query,
            //then do a filter for the developer so that only records matching the sent value is sent back to browser
            if (Query.Contains("@Const_SearchValue") == false && string.IsNullOrEmpty(SearchValue) == false)
            {
                try
                {
                    records = records.Select(Component.ValueField + "='" + SearchValue + "'").CopyToDataTable();
                }
                catch { records.Rows.Clear(); }
            }

            //for autocomplete, if searchtext was sent from browser and developer did not programmatically use the @Const_SearchText in query,
            //then do a filter for the developer so that only records matching the sent text is sent back to browser
            if (Query.Contains("@Const_SearchText") == false && string.IsNullOrEmpty(SearchText) == false)
            {
                try
                {
                    records = records.Select(Component.DisplayField + " like '%" + SearchText + "%'").CopyToDataTable();
                }
                catch { records.Rows.Clear(); }
            }


            int index = 0;
            List = new ControlList[records.Rows.Count];
            foreach (DataRow row in records.Rows)
            {
                ControlList item = new ControlList();
                item.Key = row[Component.DisplayField].ToString();
                item.Value = row[Component.ValueField].ToString();
                List[index] = item;
                index += 1;
            }

            return List;
        }

        private static async Task<string> postUrl(string url, string endpoint, object request)
        {
            HttpClient client = new HttpClient();
            var myContent = Newtonsoft.Json.JsonConvert.SerializeObject(request);
            var buffer = System.Text.Encoding.UTF8.GetBytes(myContent);
            var byteContent = new ByteArrayContent(buffer);
            byteContent.Headers.ContentType = new MediaTypeHeaderValue("application/json");
            HttpResponseMessage response = await client.PostAsync(url + endpoint, byteContent);

            HttpContent requestContent = response.Content;
            string jsonContent = requestContent.ReadAsStringAsync().Result;
            return jsonContent;
        }

        private static object Encrypt_Hash(string Option, object Value)
        {
            if (string.IsNullOrEmpty(Option) == false)
            {
                if (Option.ToLower() == "encrypt")
                {
                    return EncryptText(Value.ToString());
                }
                else
                {
                    if (Option.ToLower() == "hash")
                    {
                        return HASH256(Value.ToString());
                    }
                    else
                    {
                        return Value;
                    }
                }
            }
            else
            {
                return Value;
            }
        }

        private static object CastDataType(string DataType, string Value)
        {
            //todo: put block in try catch and log exception when data type and value mistmtch occures
            if (string.IsNullOrEmpty(Value) == false)
            {
                switch ((string.IsNullOrEmpty(DataType) ? "" : DataType).ToLower())
                {
                    case "int32": return long.Parse(Value); break;
                    case "string": return Value; break;
                    case "decimal": return decimal.Parse(Value); break;
                    case "datetime": return DateTime.Parse(Value); break;
                    case "bool":
                    case "boolean": return bool.Parse(Value); break;
                    case "double": return double.Parse(Value); break;
                    default: return Value; break;
                }
            }

            return null;
        }

        private static object CastDataTypeDefault(string DataType, string Value)
        {
            //todo: put block in try catch and log exception when data type and value mistmtch occures
            if (string.IsNullOrEmpty(Value) == false)
            {
                switch ((string.IsNullOrEmpty(DataType) ? "" : DataType).ToLower())
                {
                    case "int32": return long.Parse(Value); break;
                    case "string": return Value; break;
                    case "decimal": return decimal.Parse(Value); break;
                    case "datetime": return DateTime.Parse(Value); break;
                    case "bool":
                    case "boolean": return bool.Parse(Value); break;
                    case "double": return double.Parse(Value); break;
                    default: return Value; break;
                }
            }
            else
            {
                switch ((string.IsNullOrEmpty(DataType) ? "" : DataType).ToLower())
                {
                    case "int32": return long.Parse("0"); break;
                    case "string": return ""; break;
                    case "decimal": return decimal.Parse("0"); break;
                    case "datetime": return DateTime.Parse("01/01/1900"); break;
                    case "bool":
                    case "boolean": return false; break;
                    case "double": return double.Parse("0"); break;
                    default: return DBNull.Value; break;
                }
            }
        }

        private static async Task<DataSet> ExecuteQuery(Component Component, string Query, UserSession Session, params SqlParameter[] Params)
        {
            List<SqlParameter> list = new List<SqlParameter>();

            if (Params != null)
                list = Params.ToList();

            return await ConnnectDB(Component.ConnectionString, Component.ConnectionTimeout, Query, Session, list);
        }

        private static async Task<DataSet> ConnnectDB(string connectionstring, int connTimeout, string Query, UserSession Session, List<SqlParameter> Params)
        {
            DataSet ds = new DataSet();

            if (Session != null)
            {
                if (Params == null || Params.Count == 0)
                {
                    Params = new List<SqlParameter>() {
                    new SqlParameter("@Const_SessionID", Session.SessionID),
                    new SqlParameter("@Const_UserName", Session.UserName),
                    new SqlParameter("@Const_TenantID", Session.TenantID),
                };
                }
                else
                {
                    //remove null parameters
                    Params = Params.Where(param => param != null).ToList();

                    if (Session != null && Session.SessionID != null)
                    {
                        if (Params.Where(param => param.ParameterName.ToLower() == "@const_sessionid").Count() == 0)
                            Params.Add(new SqlParameter("@Const_SessionID", Session.SessionID));
                    }

                    if (Params.Where(param => param.ParameterName.ToLower() == "@const_username").Count() == 0)
                        Params.Add(new SqlParameter("@Const_UserName", Session.UserName));

                    if (Params.Where(param => param.ParameterName.ToLower() == "@const_tenantid").Count() == 0)
                        Params.Add(new SqlParameter("@Const_TenantID", Session.TenantID));
                }
            }

            List<SqlParameter> @params = Params;


            string engine = "ms sqlserver";

            switch (engine.ToLower())
            {
                case "ms sqlserver":
                    SqlCommand sqlCommand = new SqlCommand(Query.ToString(), new SqlConnection(connectionstring));
                    sqlCommand.CommandTimeout = connTimeout;


                    if (Params != null)
                    {
                        for (int i = 0; i < (int)@params.Count; i++)
                        {
                            if (@params[i] != null)
                            {
                                SqlParameter sqlParameter = new SqlParameter(@params[i].ParameterName, @params[i].Value);
                                sqlCommand.Parameters.Add(sqlParameter);
                            }
                        }
                    }

                    SqlDataAdapter sqlDataAdapter = new SqlDataAdapter(sqlCommand);
                    DataSet dMSSQLTable = new DataSet();

                    sqlDataAdapter.Fill(dMSSQLTable);
                    return dMSSQLTable;
                    break;
            }

            return new DataSet();
        }

        internal static async Task<DataTable> ExecQuery(string connectionstring, string Query, List<SqlParameter> Params)
        {
            DataSet ds = new DataSet();
            List<SqlParameter> @params = Params;
            string engine = "ms sqlserver";

            switch (engine.ToLower())
            {
                case "ms sqlserver":
                    SqlCommand sqlCommand = new SqlCommand(Query.ToString(), new SqlConnection(connectionstring));

                    if (Params != null)
                    {
                        for (int i = 0; i < (int)@params.Count; i++)
                        {
                            if (@params[i] != null)
                            {
                                SqlParameter sqlParameter = new SqlParameter(@params[i].ParameterName, @params[i].Value);
                                sqlCommand.Parameters.Add(sqlParameter);
                            }
                        }
                    }

                    SqlDataAdapter sqlDataAdapter = new SqlDataAdapter(sqlCommand);
                    DataTable dMSSQLTable = new DataTable();

                    sqlDataAdapter.Fill(dMSSQLTable);
                    return dMSSQLTable;
                    break;
            }

            return new DataTable();
        }

        internal static DataTable ExecQuery_Sync(string connectionstring, string Query, List<SqlParameter> Params)
        {
            DataSet ds = new DataSet();
            List<SqlParameter> @params = Params;
            string engine = "ms sqlserver";

            switch (engine.ToLower())
            {
                case "ms sqlserver":
                    SqlCommand sqlCommand = new SqlCommand(Query.ToString(), new SqlConnection(connectionstring));

                    if (Params != null)
                    {
                        for (int i = 0; i < (int)@params.Count; i++)
                        {
                            if (@params[i] != null)
                            {
                                SqlParameter sqlParameter = new SqlParameter(@params[i].ParameterName, @params[i].Value);
                                sqlCommand.Parameters.Add(sqlParameter);
                            }
                        }
                    }

                    SqlDataAdapter sqlDataAdapter = new SqlDataAdapter(sqlCommand);
                    DataTable dMSSQLTable = new DataTable();

                    sqlDataAdapter.Fill(dMSSQLTable);
                    return dMSSQLTable;
                    break;
            }

            return new DataTable();
        }

        internal static async Task QueryAsync(Application Application, string ConnectionString, string Query, List<SqlParameter> Parameters)
        {
            await QuickProcess.Service.ExecQuery(ConnectionString, Query, Parameters);
        }

        internal static async Task<IEnumerable<T>> QueryAsync<T>(Application Application, string ConnectionString, string Query, List<SqlParameter> Parameters)
        {
            return await TableToObject<T>(await QuickProcess.Service.ExecQuery(ConnectionString, Query, Parameters));
        }

        internal static async Task<IEnumerable<T>> TableToObject<T>(DataTable table)
        {
            List<dynamic> ObjectList = new List<dynamic>();


            foreach (DataRow row in table.Rows)
            {
                dynamic obj = new ExpandoObject();
                ObjectList.Add(obj);
                foreach (DataColumn col in table.Columns)
                {
                    var dic = (IDictionary<string, object>)obj;



                    if (string.IsNullOrEmpty(row[col.ColumnName].ToString().Replace(" ", "")))
                    {
                        switch (col.DataType.Name.ToString().ToLower())
                        {
                            case "int":
                            case "int32":
                            case "int64":
                                dic[col.ColumnName.Replace(" ", "")] = 0;
                                break;

                            case "boolean":
                                dic[col.ColumnName.Replace(" ", "")] = false;
                                break;

                            case "string":
                                dic[col.ColumnName.Replace(" ", "")] = row[col.ColumnName];
                                break;
                        }
                    }
                    else
                    {
                        dic[col.ColumnName.Replace(" ", "")] = row[col.ColumnName];
                    }
                }
            }
            return Newtonsoft.Json.JsonConvert.DeserializeObject<List<T>>(Newtonsoft.Json.JsonConvert.SerializeObject(ObjectList));
        }

        internal static async Task<Component> getComponent(Application Application, string componentName)
        {
            var comp = new Component();

            try
            {

                string FolderPath = Application.ComponentList.Where(component => component.Name.ToLower() == componentName.ToLower()).FirstOrDefault().FolderPath;


                string filePath = "Components\\" + FolderPath;
                comp = System.Text.Json.JsonSerializer.Deserialize<Component>(await readFile(filePath, componentName + ".json"));
                var conn = Application.getConnection(comp.Connection.GUID);


                if (conn != null)
                {
                    comp.ConnectionString = conn.ConnectionString;
                    comp.Connection.Engine = conn.Engine;
                }
                return comp;
            }
            catch (Exception ex)
            {
                //logger((new StackTrace()).GetFrame(0).GetMethod().ToString(), ex.Message, (new StackTrace()).GetFrame(0).GetILOffset().ToString(), DateTime.Now.ToString() + ":" + ex.StackTrace, 0, "getApplicationSettings");
            }
            return null;
        }

        private static async Task<QuickProcess.ConnectionList> getConnectionString(string ConnectionGUID, List<QuickProcess.ConnectionList> ConnectionList)
        {
            if (ConnectionGUID != null)
            {
                foreach (var conn in ConnectionList)
                {
                    if (conn.GUID.ToLower() == ConnectionGUID.ToLower())
                        return conn;
                }
            }

            return null;
        }



        internal static string HASH256(string text)
        {
            return string.IsNullOrEmpty(text) ? string.Empty : BitConverter.ToString(new System.Security.Cryptography.SHA256Managed().ComputeHash(System.Text.Encoding.UTF8.GetBytes(text))).Replace("-", string.Empty); ;
        }

        internal static string EncryptText(string clearText)
        {
            string EncryptionKey = "encryptionKEY--Quick==Process";
            byte[] clearBytes = Encoding.Unicode.GetBytes(clearText);
            using (Aes encryptor = Aes.Create())
            {
                Rfc2898DeriveBytes pdb = new Rfc2898DeriveBytes(EncryptionKey, new byte[] { 0x49, 0x76, 0x61, 0x6e, 0x20, 0x4d, 0x65, 0x64, 0x76, 0x65, 0x64, 0x65, 0x76 });
                encryptor.Key = pdb.GetBytes(32);
                encryptor.IV = pdb.GetBytes(16);
                using (MemoryStream ms = new MemoryStream())
                {
                    using (CryptoStream cs = new CryptoStream(ms, encryptor.CreateEncryptor(), CryptoStreamMode.Write))
                    {
                        cs.Write(clearBytes, 0, clearBytes.Length);
                        cs.Close();
                    }
                    clearText = Convert.ToBase64String(ms.ToArray());
                }
            }
            return clearText;
        }

        internal static string DecryptText(string cipherText)
        {
            string EncryptionKey = "encryptionKEY--Quick==Process";
            cipherText = cipherText.Replace(" ", "+");
            byte[] cipherBytes = Convert.FromBase64String(cipherText);
            using (Aes encryptor = Aes.Create())
            {
                Rfc2898DeriveBytes pdb = new Rfc2898DeriveBytes(EncryptionKey, new byte[] { 0x49, 0x76, 0x61, 0x6e, 0x20, 0x4d, 0x65, 0x64, 0x76, 0x65, 0x64, 0x65, 0x76 });
                encryptor.Key = pdb.GetBytes(32);
                encryptor.IV = pdb.GetBytes(16);
                using (MemoryStream ms = new MemoryStream())
                {
                    using (CryptoStream cs = new CryptoStream(ms, encryptor.CreateDecryptor(), CryptoStreamMode.Write))
                    {
                        cs.Write(cipherBytes, 0, cipherBytes.Length);
                        cs.Close();
                    }
                    cipherText = Encoding.Unicode.GetString(ms.ToArray());
                }
            }
            return cipherText;
        }

        private static async Task<string> readFile(string containerName, string fileName)
        {
            var binDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            var rootDirectory = Path.GetFullPath(Path.Combine(binDirectory, ".."));
            string path = "";
            if (Directory.Exists(binDirectory + "\\QuickProcess\\"))
                path = binDirectory + "\\QuickProcess\\";

            if (Directory.Exists(rootDirectory + "\\QuickProcess\\"))
                path = rootDirectory + "\\QuickProcess\\";

            string file = System.IO.File.ReadAllText(path + containerName + "\\" + fileName);
            return file;
        }

    }
}
