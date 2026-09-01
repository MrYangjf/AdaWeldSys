using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace AdaWeldSystem.EmguALG.EmguConfiger
{
    /// <summary>
    /// 算法 Job 管理器：多 Job 增删改查与运行时切换，配置持久化到 Config/Jobs/
    /// </summary>
    public sealed class JobManager
    {
        private static readonly Lazy<JobManager> _instance = new Lazy<JobManager>(() => new JobManager());
        public static JobManager Instance => _instance.Value;

        private readonly Dictionary<int, AlgorithmJob> _jobs = new Dictionary<int, AlgorithmJob>();
        private int _currentJobId = 1;

        /// <summary>当前 Job 编号</summary>
        public int CurrentJobId => _currentJobId;

        /// <summary>当前 Job 对象</summary>
        public AlgorithmJob CurrentJob => _jobs.ContainsKey(_currentJobId) ? _jobs[_currentJobId] : null;

        /// <summary>所有 Job 的只读集合</summary>
        public IReadOnlyDictionary<int, AlgorithmJob> Jobs => _jobs;

        /// <summary>Job 切换事件：切换完成后触发</summary>
        public event EventHandler<JobSwitchedEventArgs> JobSwitched;

        /// <summary>Job 数据变更事件：增删改后触发</summary>
        public event EventHandler JobListChanged;

        private readonly string _jobsDir;
        private readonly string _indexFile;

        private JobManager()
        {
            _jobsDir = Path.Combine(Application.StartupPath, "Config", "Jobs");
            _indexFile = Path.Combine(_jobsDir, "JobIndex.ini");
        }

        /// <summary>加载全部 Job 并应用当前配置到 PipelineManager。</summary>
        public void Initialize()
        {
            LoadJobsFromIni();
            ApplyCurrentJob();
        }

        /// <summary>切换当前 Job：将目标 Job 配置应用到 PipelineManager，并持久化当前 JobId。</summary>
        /// <param name="jobId">目标 Job 编号</param>
        public void SwitchJob(int jobId)
        {
            if (!_jobs.ContainsKey(jobId))
                throw new InvalidOperationException(string.Format("Job {0} 不存在", jobId));

            // 保存当前 Job 配置，避免丢失未持久化的修改
            SaveCurrentJobConfig();

            _currentJobId = jobId;
            ApplyCurrentJob();

            // 持久化当前 Job ID
            SaveIndex();

            JobSwitched?.Invoke(this, new JobSwitchedEventArgs(jobId, _jobs[jobId].JobName));
        }

        /// <summary>添加新 Job 并持久化。</summary>
        public void AddJob(AlgorithmJob job)
        {
            if (job == null)
                throw new ArgumentNullException("job");
            if (_jobs.ContainsKey(job.JobId))
                throw new InvalidOperationException(string.Format("Job ID {0} 已存在", job.JobId));

            _jobs[job.JobId] = job;
            SaveJobToFile(job);
            SaveIndex();
            JobListChanged?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>更新指定 Job 的配置（深拷贝）并持久化，不切换当前 Job。</summary>
        /// <param name="jobId">目标 Job 编号</param>
        /// <param name="config">新配置</param>
        public void UpdateJobConfig(int jobId, PipelineConfig config)
        {
            if (!_jobs.TryGetValue(jobId, out var job))
                throw new InvalidOperationException(string.Format("Job {0} 不存在", jobId));

            job.Config = config?.Clone() ?? new PipelineConfig();
            SaveJobToFile(job);
        }

        /// <summary>更新 Job 名称并持久化。</summary>
        /// <param name="jobId">目标 Job 编号</param>
        /// <param name="name">新名称</param>
        public void UpdateJobName(int jobId, string name)
        {
            if (!_jobs.TryGetValue(jobId, out var job))
                throw new InvalidOperationException(string.Format("Job {0} 不存在", jobId));

            job.JobName = name ?? string.Format("Job_{0:D3}", jobId);
            SaveIndex();
            JobListChanged?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>删除指定 Job（至少保留一个），若删除当前 Job 则自动切到首个可用 Job。</summary>
        /// <param name="jobId">目标 Job 编号</param>
        public void RemoveJob(int jobId)
        {
            if (_jobs.Count <= 1)
                throw new InvalidOperationException("至少保留一个 Job");
            if (!_jobs.ContainsKey(jobId))
                return;

            _jobs.Remove(jobId);

            // 删除对应的 INI 文件
            var jobFile = GetJobFilePath(jobId);
            if (File.Exists(jobFile))
                File.Delete(jobFile);

            // 如果删除的是当前 Job，切换到第一个可用 Job
            if (_currentJobId == jobId)
            {
                _currentJobId = _jobs.Keys.OrderBy(k => k).First();
                ApplyCurrentJob();
            }

            SaveIndex();
            JobListChanged?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>保存当前 Job 配置到内存（从 PipelineManager 同步）。</summary>
        public void SaveCurrentJobConfig()
        {
            if (_jobs.TryGetValue(_currentJobId, out var job))
            {
                job.Config = PipelineManager.Instance.Config.Clone();
            }
        }

        /// <summary>保存所有 Job 到 INI 文件（同步当前 PipelineManager 配置后批量写入）。</summary>
        public void SaveAllJobs()
        {
            SaveCurrentJobConfig();
            Directory.CreateDirectory(_jobsDir);
            foreach (var pair in _jobs)
            {
                SaveJobToFile(pair.Value);
            }
            SaveIndex();
        }

        #region 私有方法

        private void ApplyCurrentJob()
        {
            if (_jobs.TryGetValue(_currentJobId, out var job) && job.Config != null)
            {
                PipelineManager.Instance.SetConfig(job.Config.Clone());
            }
        }

        private void LoadJobsFromIni()
        {
            _jobs.Clear();

            if (!File.Exists(_indexFile))
            {
                // 首次运行：创建默认 Job
                CreateDefaultJob();
                return;
            }

            var indexIni = new AdaWeldSystem.FileOperate.INIFile(_indexFile);
            var idsStr = indexIni.ReadString("JobList", "Ids", "1");
            _currentJobId = indexIni.ReadInt("JobList", "CurrentId", 1);

            var ids = idsStr.Split(',').Select(s => int.Parse(s.Trim())).ToArray();
            foreach (var id in ids)
            {
                var job = LoadJobFromFile(id);
                if (job != null)
                    _jobs[id] = job;
            }

            // 如果没有任何 Job 加载成功，创建默认 Job
            if (_jobs.Count == 0)
            {
                CreateDefaultJob();
            }
        }

        private void CreateDefaultJob()
        {
            var defaultJob = new AlgorithmJob
            {
                JobId = 1,
                JobName = "Default",
                Config = new PipelineConfig()
            };
            _jobs[1] = defaultJob;
            _currentJobId = 1;
            Directory.CreateDirectory(_jobsDir);
            SaveJobToFile(defaultJob);
            SaveIndex();
        }

        private void SaveIndex()
        {
            Directory.CreateDirectory(_jobsDir);
            var indexIni = new AdaWeldSystem.FileOperate.INIFile(_indexFile);
            var idsStr = string.Join(",", _jobs.Keys.OrderBy(k => k));
            indexIni.WriteString("JobList", "Ids", idsStr);
            indexIni.WriteInt("JobList", "CurrentId", _currentJobId);
            indexIni.SaveToFile();
        }

        private string GetJobFilePath(int jobId)
        {
            return Path.Combine(_jobsDir, string.Format("Job_{0:D3}.ini", jobId));
        }

        private void SaveJobToFile(AlgorithmJob job)
        {
            if (job == null) return;
            var filePath = GetJobFilePath(job.JobId);
            Directory.CreateDirectory(_jobsDir);

            var ini = new AdaWeldSystem.FileOperate.INIFile(filePath);
            ini.WriteInt("JobInfo", "Id", job.JobId);
            ini.WriteString("JobInfo", "Name", job.JobName);

            PipelineManager.SaveConfigToIni(ini, job.Config);
            ini.SaveToFile();
        }

        private AlgorithmJob LoadJobFromFile(int jobId)
        {
            var filePath = GetJobFilePath(jobId);
            if (!File.Exists(filePath))
                return null;

            try
            {
                var ini = new AdaWeldSystem.FileOperate.INIFile(filePath);
                var job = new AlgorithmJob
                {
                    JobId = ini.ReadInt("JobInfo", "Id", jobId),
                    JobName = ini.ReadString("JobInfo", "Name", string.Format("Job_{0:D3}", jobId)),
                    Config = PipelineManager.LoadConfigFromIni(ini)
                };
                return job;
            }
            catch
            {
                return null;
            }
        }

        #endregion
    }

    /// <summary>
    /// Job 切换事件参数
    /// </summary>
    public class JobSwitchedEventArgs : EventArgs
    {
        public int JobId { get; }
        public string JobName { get; }

        public JobSwitchedEventArgs(int jobId, string jobName)
        {
            JobId = jobId;
            JobName = jobName;
        }
    }
}