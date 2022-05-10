﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Diagnostics;
using System.Data.Common;
using System.Globalization;
using inercya.EntityLite.SqliteProfiler.Entities;
using System.IO;
using inercya.EntityLite.Collections;
using inercya.EntityLite.Extensions;
using Microsoft.Extensions.Logging;

namespace inercya.EntityLite.SqliteProfiler
{
    public class LogItem
    {
        public string CommandText;
        public TimeSpan ExecutionTime;
        public string Params;
        public string ApplicationContext;
        public Guid DataServiceInstanceId;
    }

    public enum ProfileFileFrecuency
    {
        Daily,
        Weekly,
        Monthly
    }


    public class Profiler : inercya.EntityLite.IProfilerLite
    {
        private static ILogger logger;

        private static bool isLoggerInitialized = false;
        private static ILogger Log
        {
            get
            {
                if (!isLoggerInitialized)
                {
                    isLoggerInitialized = true;
                    try
                    {
                        logger = ConfigurationLite.LoggerFactory.CreateLogger<Profiler>();
                    }
                    catch { }
                }
                return logger;
            }
        }

        SafeQueue<LogItem> logItems;
        private AutoResetEvent signal;
        private volatile bool _isRunning;
        private object syncObject = new object();
        private Thread workingThread;

        public readonly ProfileFileFrecuency ProfileDatabaseFrecuency;
        public readonly string ProfileDatabaseFileNamePrefix;
        public readonly int MaxProfileDatabaseFiles;
        public readonly string ProfileDatabaseFolderPath;
        public readonly bool FullLogging;

        private DateTime lastDataServiceCreateDay;

        public bool IsRunning
        {
            get { return _isRunning; }
            private set { _isRunning = value; }
        }


        public Profiler(
            string profileDatabaseFolderPath,
            ProfileFileFrecuency profileFileFrecuency = ProfileFileFrecuency.Weekly,
            bool fullLogging = false,
            int maxProfileDatabaseFiles = 6,
            string profileDatabaseFileNamePrefix = "EntityLiteProfile-")
        {
            this.ProfileDatabaseFolderPath = profileDatabaseFolderPath;
            this.ProfileDatabaseFrecuency = profileFileFrecuency;
            this.MaxProfileDatabaseFiles = maxProfileDatabaseFiles;
            this.ProfileDatabaseFileNamePrefix = profileDatabaseFileNamePrefix;
            this.FullLogging = fullLogging;
 
            logItems = new SafeQueue<LogItem>();
            signal = new AutoResetEvent(false);
        }

        public void StartProfiling()
        {
            if (IsRunning)
            {
                throw new InvalidOperationException("Already profiling");
            }
            lock (syncObject)
            {
                if (IsRunning) throw new InvalidOperationException("Already profiling");
                IsRunning = true;
                workingThread = new Thread(ProcessLogItems);
                workingThread.Start();
            }
        }

        private string GetDatabaseFilePath()
        {
            DateTime today = DateTime.Today;
            switch (this.ProfileDatabaseFrecuency)
            {
                case ProfileFileFrecuency.Daily:
                    return Path.Combine(this.ProfileDatabaseFolderPath, string.Format("{0}{1}.db", this.ProfileDatabaseFileNamePrefix, today.ToString("yyyy-MM-dd")));
                case ProfileFileFrecuency.Weekly:
                    return Path.Combine(this.ProfileDatabaseFolderPath, string.Format("{0}{1:0000}-W{2:00}.db", this.ProfileDatabaseFileNamePrefix, today.Year,
                        CultureInfo.InvariantCulture.Calendar.GetWeekOfYear(today, CalendarWeekRule.FirstDay, DayOfWeek.Monday)));
                case ProfileFileFrecuency.Monthly:
                    return Path.Combine(this.ProfileDatabaseFolderPath, string.Format("{0}{1}.db", this.ProfileDatabaseFileNamePrefix, today.ToString("yyyy-'M'MM")));
                default:
                    throw new InvalidOperationException("Invalid ProfileDatabaseFrecuency");
            }
        }



        public void LogCommandExecution(DbCommand command, DataService dataService, TimeSpan executionTime)
        {
            if (!IsRunning || dataService is SqliteProfilerDataService) return;
            var item = new LogItem
            {
                CommandText = command.CommandText,
                ExecutionTime = executionTime,
                Params = command.GetParamsAsString(),
                ApplicationContext = dataService.ApplicationContextGetter?.Invoke(),
                DataServiceInstanceId = dataService.InstanceId
            };
            logItems.Enqueue(item);
            signal.Set();
        }

        public void StopProfiling()
        {
            if (!IsRunning) throw new InvalidOperationException("Already stopped");
            lock (syncObject)
            {
                if (!IsRunning) throw new InvalidOperationException("Already stopped");
                IsRunning = false;
                signal.Set();
                workingThread.Join();
                workingThread = null;
            }
        }

        private void ProcessLogItems()
        {
            SqliteProfilerDataService dataService = null;
            try
            {
                while (true)
                {
                    signal.WaitOne();
                    dataService = EnsureDataServiceAndDeleteOldFiles(dataService);
                    try
                    {
                        ProcessLogItems(dataService);
                    }
                    catch (Exception ex)
                    {
                        Log?.LogError(ex, "Error Profiling");
                    }
                    if (!IsRunning)
                    {
                        TryDisposeDataService(dataService);
                        dataService = null;
                        return;
                    }
                }
            }
            catch (Exception ex)
            {
                Log?.LogError(ex, "Error Profiling");
            }
            finally
            {
                this.IsRunning = false;
                TryDisposeDataService(dataService);
                dataService = null;
            }
        }

        private void ProcessLogItems(SqliteProfilerDataService dataService)
        {
            LogItem item = null;
            while (!logItems.IsEmpty)
            {
                dataService.BeginTransaction();
                int itemCount = 0;
                while (logItems.Dequeue(out item))
                {
                    dataService.LogCommandExecution(item, this.FullLogging);
                    itemCount++;
                }
                dataService.Commit();
            }
        }

        private static void TryDisposeDataService(SqliteProfilerDataService dataService)
        {
            if (dataService != null)
            {
                try
                {
                    dataService.Dispose();
                }
                catch (Exception ex)
                {
                    Log?.LogError(ex, "Error disposing data service");
                }
                dataService = null;
            }
        }

        private SqliteProfilerDataService EnsureDataServiceAndDeleteOldFiles(SqliteProfilerDataService dataService)
        {
            string filePath = null;
            if (dataService == null || (DateTime.Today != this.lastDataServiceCreateDay && dataService.FilePath != (filePath = this.GetDatabaseFilePath())))
            {
                TryDisposeDataService(dataService);
                if (filePath == null) filePath = this.GetDatabaseFilePath();
                int attempt = 0;
                while (true)
                {
                    try
                    {
                        dataService = SqliteProfilerDataService.Create(filePath);
                        lastDataServiceCreateDay = DateTime.Today;
                        dataService.OpenConnection();
                    }
                    catch
                    {
                        TryDisposeDataService(dataService);
                        if (++attempt >= 20) throw;
                        Thread.Sleep(250);
                    }
                }
            }
            return dataService;
        }

        private void DeleteOldFiles(object state)
        {
            try
            {
                var fileInfos = new List<FileInfo>();
                var files = Directory.GetFiles(this.ProfileDatabaseFolderPath, this.ProfileDatabaseFileNamePrefix + "*.db");
                if (files.Length <= this.MaxProfileDatabaseFiles) return;
                foreach (var filePah in files)
                {
                    fileInfos.Add(new FileInfo(filePah));
                }
                fileInfos.Sort((fi1, fi2) => fi1.LastWriteTime.CompareTo(fi2.LastWriteTime));
                for (int i = 0; i < fileInfos.Count - this.MaxProfileDatabaseFiles; i++)
                {
                    fileInfos[i].Delete();
                }
            }
            catch (Exception ex)
            {
                Log?.LogError(ex, "Error deleting old profiler database files");
            }
        }
    }
}
