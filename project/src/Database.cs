// Database.cs - Высокопроизводительная обертка над нативной SQLite (winsqlite3.dll)
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace PizzaSystem.Data
{
    public class SQLiteDatabase : IDisposable
    {
        private IntPtr _db = IntPtr.Zero;
        private readonly object _lock = new object();
        private readonly string _dbPath;

        #region Native Imports (winsqlite3.dll - встроенная библиотека Windows)
        [DllImport("winsqlite3.dll", EntryPoint = "sqlite3_open16", CallingConvention = CallingConvention.Cdecl)]
        private static extern int sqlite3_open16([MarshalAs(UnmanagedType.LPWStr)] string filename, out IntPtr db);

        [DllImport("winsqlite3.dll", EntryPoint = "sqlite3_close", CallingConvention = CallingConvention.Cdecl)]
        private static extern int sqlite3_close(IntPtr db);

        [DllImport("winsqlite3.dll", EntryPoint = "sqlite3_exec", CallingConvention = CallingConvention.Cdecl)]
        private static extern int sqlite3_exec(IntPtr db, byte[] sql, IntPtr callback, IntPtr arg, out IntPtr errMsg);

        [DllImport("winsqlite3.dll", EntryPoint = "sqlite3_prepare16_v2", CallingConvention = CallingConvention.Cdecl)]
        private static extern int sqlite3_prepare16_v2(IntPtr db, [MarshalAs(UnmanagedType.LPWStr)] string sql, int numBytes, out IntPtr stmt, IntPtr tail);

        [DllImport("winsqlite3.dll", EntryPoint = "sqlite3_step", CallingConvention = CallingConvention.Cdecl)]
        private static extern int sqlite3_step(IntPtr stmt);

        [DllImport("winsqlite3.dll", EntryPoint = "sqlite3_finalize", CallingConvention = CallingConvention.Cdecl)]
        private static extern int sqlite3_finalize(IntPtr stmt);

        [DllImport("winsqlite3.dll", EntryPoint = "sqlite3_column_count", CallingConvention = CallingConvention.Cdecl)]
        private static extern int sqlite3_column_count(IntPtr stmt);

        [DllImport("winsqlite3.dll", EntryPoint = "sqlite3_column_name16", CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr sqlite3_column_name16(IntPtr stmt, int index);

        [DllImport("winsqlite3.dll", EntryPoint = "sqlite3_column_type", CallingConvention = CallingConvention.Cdecl)]
        private static extern int sqlite3_column_type(IntPtr stmt, int index);

        [DllImport("winsqlite3.dll", EntryPoint = "sqlite3_column_int64", CallingConvention = CallingConvention.Cdecl)]
        private static extern long sqlite3_column_int64(IntPtr stmt, int index);

        [DllImport("winsqlite3.dll", EntryPoint = "sqlite3_column_double", CallingConvention = CallingConvention.Cdecl)]
        private static extern double sqlite3_column_double(IntPtr stmt, int index);

        [DllImport("winsqlite3.dll", EntryPoint = "sqlite3_column_text16", CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr sqlite3_column_text16(IntPtr stmt, int index);

        [DllImport("winsqlite3.dll", EntryPoint = "sqlite3_bind_text16", CallingConvention = CallingConvention.Cdecl)]
        private static extern int sqlite3_bind_text16(IntPtr stmt, int index, [MarshalAs(UnmanagedType.LPWStr)] string val, int numBytes, IntPtr destructor);

        [DllImport("winsqlite3.dll", EntryPoint = "sqlite3_bind_int64", CallingConvention = CallingConvention.Cdecl)]
        private static extern int sqlite3_bind_int64(IntPtr stmt, int index, long val);

        [DllImport("winsqlite3.dll", EntryPoint = "sqlite3_bind_double", CallingConvention = CallingConvention.Cdecl)]
        private static extern int sqlite3_bind_double(IntPtr stmt, int index, double val);

        [DllImport("winsqlite3.dll", EntryPoint = "sqlite3_bind_null", CallingConvention = CallingConvention.Cdecl)]
        private static extern int sqlite3_bind_null(IntPtr stmt, int index);

        [DllImport("winsqlite3.dll", EntryPoint = "sqlite3_last_insert_rowid", CallingConvention = CallingConvention.Cdecl)]
        private static extern long sqlite3_last_insert_rowid(IntPtr db);

        [DllImport("winsqlite3.dll", EntryPoint = "sqlite3_errmsg16", CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr sqlite3_errmsg16(IntPtr db);

        private const int SQLITE_OK = 0;
        private const int SQLITE_ROW = 100;
        private const int SQLITE_DONE = 101;
        private const int SQLITE_INTEGER = 1;
        private const int SQLITE_FLOAT = 2;
        private const int SQLITE_TEXT = 3;
        private const int SQLITE_BLOB = 4;
        private const int SQLITE_NULL = 5;
        #endregion

        public SQLiteDatabase(string dbPath)
        {
            _dbPath = dbPath;
            int rc = sqlite3_open16(_dbPath, out _db);
            if (rc != SQLITE_OK)
            {
                throw new Exception("Не удалось открыть базу данных SQLite: " + GetLastError());
            }

            ExecuteScript("PRAGMA journal_mode = WAL; PRAGMA synchronous = NORMAL; PRAGMA foreign_keys = ON;");
        }

        private string GetLastError()
        {
            if (_db == IntPtr.Zero) return "БД не инициализирована";
            IntPtr errPtr = sqlite3_errmsg16(_db);
            return errPtr != IntPtr.Zero ? Marshal.PtrToStringUni(errPtr) : "Неизвестная ошибка";
        }

        public void ExecuteScript(string sqlScript)
        {
            lock (_lock)
            {
                byte[] bytes = Encoding.UTF8.GetBytes(sqlScript + "\0");
                IntPtr errPtr;
                int rc = sqlite3_exec(_db, bytes, IntPtr.Zero, IntPtr.Zero, out errPtr);
                if (rc != SQLITE_OK)
                {
                    string err = errPtr != IntPtr.Zero ? Marshal.PtrToStringAnsi(errPtr) : GetLastError();
                    throw new Exception("Ошибка выполнения SQL скрипта: " + err);
                }
            }
        }

        public int ExecuteNonQuery(string sql, params object[] parameters)
        {
            lock (_lock)
            {
                IntPtr stmt;
                int rc = sqlite3_prepare16_v2(_db, sql, -1, out stmt, IntPtr.Zero);
                if (rc != SQLITE_OK)
                {
                    throw new Exception("Ошибка компиляции SQL: " + GetLastError() + "\nSQL: " + sql);
                }

                try
                {
                    BindParameters(stmt, parameters);
                    rc = sqlite3_step(stmt);
                    if (rc != SQLITE_DONE && rc != SQLITE_ROW)
                    {
                        throw new Exception("Ошибка выполнения SQL: " + GetLastError() + "\nSQL: " + sql);
                    }
                    return 1;
                }
                finally
                {
                    sqlite3_finalize(stmt);
                }
            }
        }

        public long InsertAndGetId(string sql, params object[] parameters)
        {
            lock (_lock)
            {
                ExecuteNonQuery(sql, parameters);
                return sqlite3_last_insert_rowid(_db);
            }
        }

        public object ExecuteScalar(string sql, params object[] parameters)
        {
            lock (_lock)
            {
                IntPtr stmt;
                int rc = sqlite3_prepare16_v2(_db, sql, -1, out stmt, IntPtr.Zero);
                if (rc != SQLITE_OK)
                {
                    throw new Exception("Ошибка компиляции SQL: " + GetLastError() + "\nSQL: " + sql);
                }

                try
                {
                    BindParameters(stmt, parameters);
                    if (sqlite3_step(stmt) == SQLITE_ROW)
                    {
                        return GetColumnValue(stmt, 0);
                    }
                    return null;
                }
                finally
                {
                    sqlite3_finalize(stmt);
                }
            }
        }

        public List<Dictionary<string, object>> Query(string sql, params object[] parameters)
        {
            lock (_lock)
            {
                var result = new List<Dictionary<string, object>>();
                IntPtr stmt;
                int rc = sqlite3_prepare16_v2(_db, sql, -1, out stmt, IntPtr.Zero);
                if (rc != SQLITE_OK)
                {
                    throw new Exception("Ошибка подготовки запроса: " + GetLastError() + "\nSQL: " + sql);
                }

                try
                {
                    BindParameters(stmt, parameters);
                    int colCount = sqlite3_column_count(stmt);
                    string[] colNames = new string[colCount];
                    for (int i = 0; i < colCount; i++)
                    {
                        IntPtr namePtr = sqlite3_column_name16(stmt, i);
                        colNames[i] = Marshal.PtrToStringUni(namePtr);
                    }

                    while (sqlite3_step(stmt) == SQLITE_ROW)
                    {
                        var row = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
                        for (int i = 0; i < colCount; i++)
                        {
                            row[colNames[i]] = GetColumnValue(stmt, i);
                        }
                        result.Add(row);
                    }
                    return result;
                }
                finally
                {
                    sqlite3_finalize(stmt);
                }
            }
        }

        private void BindParameters(IntPtr stmt, object[] parameters)
        {
            if (parameters == null) return;
            for (int i = 0; i < parameters.Length; i++)
            {
                int col = i + 1;
                object val = parameters[i];
                if (val == null || val == DBNull.Value)
                {
                    sqlite3_bind_null(stmt, col);
                }
                else if (val is int || val is long || val is short || val is byte)
                {
                    sqlite3_bind_int64(stmt, col, Convert.ToInt64(val));
                }
                else if (val is bool)
                {
                    sqlite3_bind_int64(stmt, col, (bool)val ? 1 : 0);
                }
                else if (val is float || val is double || val is decimal)
                {
                    sqlite3_bind_double(stmt, col, Convert.ToDouble(val));
                }
                else if (val is DateTime)
                {
                    sqlite3_bind_text16(stmt, col, ((DateTime)val).ToString("yyyy-MM-dd HH:mm:ss"), -1, IntPtr.Zero);
                }
                else
                {
                    sqlite3_bind_text16(stmt, col, val.ToString(), -1, IntPtr.Zero);
                }
            }
        }

        private object GetColumnValue(IntPtr stmt, int col)
        {
            int type = sqlite3_column_type(stmt, col);
            switch (type)
            {
                case SQLITE_INTEGER:
                    return sqlite3_column_int64(stmt, col);
                case SQLITE_FLOAT:
                    return sqlite3_column_double(stmt, col);
                case SQLITE_TEXT:
                    IntPtr strPtr = sqlite3_column_text16(stmt, col);
                    return strPtr != IntPtr.Zero ? Marshal.PtrToStringUni(strPtr) : "";
                case SQLITE_NULL:
                    return null;
                default:
                    IntPtr defPtr = sqlite3_column_text16(stmt, col);
                    return defPtr != IntPtr.Zero ? Marshal.PtrToStringUni(defPtr) : "";
            }
        }

        public void Dispose()
        {
            lock (_lock)
            {
                if (_db != IntPtr.Zero)
                {
                    sqlite3_close(_db);
                    _db = IntPtr.Zero;
                }
            }
        }
    }
}
