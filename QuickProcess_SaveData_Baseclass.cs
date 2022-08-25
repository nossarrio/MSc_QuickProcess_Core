using System.Reflection;
using static QuickProcess.Model;

namespace QuickProcess
{
    public class QuickProcess_SaveData_Baseclass : QuickProcessBaseModel
    {
        /// <summary>
        /// saves single object properties to corresponding database table using the default connection if connection parameter is not explicitly defined
        /// </summary>
        /// <returns>returns object representation of newly saved record</returns>
        public virtual async Task<List<T>> saveObject<T>(T record, bool? AddOrUpdate = true, string? connectionString = "", [System.Runtime.CompilerServices.CallerMemberName] string callerName = "")
        {
            List<T> objectRecord = new List<T>();
            objectRecord.Add(record);
            return await saveObjects(objectRecord, AddOrUpdate, connectionString, callerName);
        }


        /// <summary>
        /// saves multiple objects properties to corresponding database table using the default connection if connection parameter is not explicitly defined
        /// </summary>
        /// <returns>returns object representation of newly saved record</returns>
        public virtual async Task<List<T>> saveObjects<T>(List<T> objects, bool? AddOrUpdate = true, string? connectionString = "", [System.Runtime.CompilerServices.CallerMemberName] string callerName = "")
        {
            string identityColumn = "";
            List<T> resultList = new List<T>();

            //verify session if underlying component is session based  and verify access to component             
            var Session = await QuickProcess_Core.getSession(SessionId.ToString(), ComponentName.ToString());
            if (Session.Item1.ResponseCode == "401")
            {
                Response = Session.Item1;
                throw new Exception("Session Expired");
            }

            if (Session.Item1.ResponseCode == "403")
            {
                Response = Session.Item1;
                throw new Exception("Access to component '" + ComponentName + "' was denied");
            }
             
  
            //generate query to get identity of newly created record
            string getIdentity = " select scope_identity() ";

            //retrieve default connection defined if no connection parameter name was sent to method
            if (string.IsNullOrEmpty(connectionString))
            {
                var connections = (await QuickProcess_Core.getApplication()).Connections.Where(conn => conn.isDefaultConnection == true).ToList();

                if (connections.Count == 0)
                {
                    throw new Exception("No default connection found in the application property file");
                }
                else
                    connectionString = connections[0].ConnectionName;
            }


            foreach (T record in objects)
            {
                bool insert = false;
                string query = "update [" + typeof(T).Name + "] set ";
                string columnPart = "(";
                string valuesPart = " values(";

                foreach (PropertyInfo prop in typeof(T).GetProperties())
                {
                    object[] attrs = prop.GetCustomAttributes(true);
                    foreach (object attr in attrs)
                    {
                        DbAttribute Attr = attr as DbAttribute;
                        if (Attr != null)
                        {
                            if (Attr.IsIdentity || Attr.KeyColumn)
                            {
                                identityColumn = prop.Name;
                                var val = prop.GetValue(record, null);
                                if (val == null || val.ToString().Replace("0", "").Replace(" ", "").Replace("null", "") == "")
                                {
                                    insert = true;
                                    query = "insert into [" + typeof(T).Name + "] ";
                                }
                                break;
                            }
                        }
                    }       
                }

                if (string.IsNullOrEmpty(identityColumn))
                {
                    throw new Exception("Object " + typeof(T).Name + " does not have an identity/key column defined. identity/Key column is required");
                }

                foreach (PropertyInfo prop in typeof(T).GetProperties())
                {
                    bool isIdentityColumn = false;
                    object[] attrs = prop.GetCustomAttributes(true);
                    foreach (object attr in attrs)
                    {
                        DbAttribute Attr = attr as DbAttribute;
                        if (Attr != null)
                        {
                            if (Attr.IsIdentity)
                            {
                                isIdentityColumn = true;
                                break;
                            }
                        }
                    }


                    if (isIdentityColumn==false)
                    {
                        if (insert)
                        {
                            if (prop.GetValue(record, null) != GetDefaultValue(prop.GetType()))
                            {
                                columnPart += "[" + prop.Name + "],";
                                valuesPart += "@" + prop.Name + ",";
                            }
                        }
                        else
                        {
                            if (AddOrUpdate.Value == true)
                            {
                                var val = prop.GetValue(record, null);
                                var defVal = GetDefaultValue(prop.GetType());

                                if (prop.GetValue(record, null) != GetDefaultValue(prop.GetType()) && prop.GetValue(record, null) != "")
                                {
                                    query += "[" + prop.Name + "]=@" + prop.Name + ",";
                                }
                            }
                            else
                                query += "[" + prop.Name + "]=@" + prop.Name + ",";
                        }
                    }
                }

                if (insert)
                {
                    columnPart = columnPart.Substring(0, columnPart.Length - 1) + ")";
                    valuesPart = valuesPart.Substring(0, valuesPart.Length - 1) + ")";
                    query += columnPart + valuesPart + getIdentity;
                }
                else
                {
                    query = query.Substring(0, query.Length - 1) + " where [" + identityColumn + "]=@" + identityColumn;
                }

                //save record to database
                resultList.Add((await QuickProcess_Core.QueryAsync<T>(SessionId, connectionString, query, record)).FirstOrDefault());
            }


            //return all populated objects
            return resultList;
        }

        object GetDefaultValue(Type t)
        {
            if (t.IsValueType)
                return Activator.CreateInstance(t);

            return null;
        }

    }
}
