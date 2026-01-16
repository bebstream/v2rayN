using System.Globalization;
using System.Reactive.Disposables.Fluent;

namespace ServiceLib.ViewModels;

public class ProfilesViewModel : MyReactiveObject
{
    #region private prop

    private List<ProfileItem> _lstProfile;
    private string _serverFilter = string.Empty;
    private Dictionary<string, bool> _dicHeaderSort = new();
    private SpeedtestService? _speedtestService;

    #endregion private prop

    #region ObservableCollection

    public IObservableCollection<ProfileItemModel> ProfileItems { get; } = new ObservableCollectionExtended<ProfileItemModel>();

    public IObservableCollection<ProfileItemModel> ProfileItemsFailedFirst { get; set; } = new ObservableCollectionExtended<ProfileItemModel>();

    public IObservableCollection<ProfileItemModel> ProfileItemsFailedLast { get; set; } = new ObservableCollectionExtended<ProfileItemModel>();

    public IObservableCollection<ProfileItemModel> ProfileItemsFailedCurrent { get; set; } = new ObservableCollectionExtended<ProfileItemModel>();

    public IObservableCollection<SubItem> SubItems { get; } = new ObservableCollectionExtended<SubItem>();

    [Reactive]
    public ProfileItemModel SelectedProfile { get; set; }

    public IList<ProfileItemModel> SelectedProfiles { get; set; }

    [Reactive]
    public SubItem SelectedSub { get; set; }

    [Reactive]
    public SubItem SelectedMoveToGroup { get; set; }

    [Reactive]
    public string ServerFilter { get; set; }

    #endregion ObservableCollection

    #region Menu

    //servers delete
    public ReactiveCommand<Unit, Unit> EditServerCmd { get; }

    public ReactiveCommand<Unit, Unit> RemoveServerCmd { get; }
    public ReactiveCommand<Unit, Unit> RemoveDuplicateServerCmd { get; }
    public ReactiveCommand<Unit, Unit> CopyServerCmd { get; }
    public ReactiveCommand<Unit, Unit> SetDefaultServerCmd { get; }
    public ReactiveCommand<Unit, Unit> ShareServerCmd { get; }
    public ReactiveCommand<Unit, Unit> GenGroupMultipleServerXrayRandomCmd { get; }
    public ReactiveCommand<Unit, Unit> GenGroupMultipleServerXrayRoundRobinCmd { get; }
    public ReactiveCommand<Unit, Unit> GenGroupMultipleServerXrayLeastPingCmd { get; }
    public ReactiveCommand<Unit, Unit> GenGroupMultipleServerXrayLeastLoadCmd { get; }
    public ReactiveCommand<Unit, Unit> GenGroupMultipleServerXrayFallbackCmd { get; }
    public ReactiveCommand<Unit, Unit> GenGroupMultipleServerSingBoxLeastPingCmd { get; }
    public ReactiveCommand<Unit, Unit> GenGroupMultipleServerSingBoxFallbackCmd { get; }

    //servers move
    public ReactiveCommand<Unit, Unit> MoveTopCmd { get; }

    public ReactiveCommand<Unit, Unit> MoveUpCmd { get; }
    public ReactiveCommand<Unit, Unit> MoveDownCmd { get; }
    public ReactiveCommand<Unit, Unit> MoveBottomCmd { get; } 
    public ReactiveCommand<SubItem, Unit> MoveToGroupCmd { get; }

    //servers ping
    public ReactiveCommand<Unit, Unit> MixedTestServerCmd { get; }
    public ReactiveCommand<Unit, Unit> AutoSpeedTestCmd { get; }
    public ReactiveCommand<Unit, Unit> TcpingServerCmd { get; }
    public ReactiveCommand<Unit, Unit> RealPingServerCmd { get; }
    public ReactiveCommand<Unit, Unit> SpeedServerCmd { get; }
    public ReactiveCommand<Unit, Unit> SortServerResultCmd { get; }
    public ReactiveCommand<Unit, Unit> RemoveInvalidServerResultCmd { get; }
    public ReactiveCommand<Unit, Unit> FastRealPingCmd { get; }

    //servers export
    public ReactiveCommand<Unit, Unit> Export2ClientConfigCmd { get; }

    public ReactiveCommand<Unit, Unit> Export2ClientConfigClipboardCmd { get; }
    public ReactiveCommand<Unit, Unit> Export2ShareUrlCmd { get; }
    public ReactiveCommand<Unit, Unit> Export2ShareUrlBase64Cmd { get; }

    public ReactiveCommand<Unit, Unit> AddSubCmd { get; }
    public ReactiveCommand<Unit, Unit> EditSubCmd { get; }
    public ReactiveCommand<Unit, Unit> DeleteSubCmd { get; }

    // Disposables
    private readonly CompositeDisposable _disposables = [];

    // 是否正处于自动测速的过程中
    private bool isInAutoSpeedTestRound = false;

    // 是否有测延迟在运行中
    private bool isDelayTestRunning = false;

    // 是否有测速度在运行中
    private bool isSpeedTestRunning = false;

    // 最小有效速度
    private readonly int minValidSpeed = 5;

    // 最小有效 server 数（达到有效速度的 server 数量）
    private readonly int minValidSpeedProfileCount = 3;

    // 最小有效 server 数
    private readonly int minValidProfileCount = 20;

    // 最大的小循环测试数，达到有效速度的 server，开启小循环测试。
    private readonly int maxItemLoopCount = 10;

    // 保存本次小循环测试的 server 数，如果超过 10，就设置为 10
    private int currentItemLoopCount = 0;

    // 到下一个整点剩余时间
    private string _timeToNextHour;
    public string TimeToNextHour
    {
        get => _timeToNextHour;
        set => this.RaiseAndSetIfChanged(ref _timeToNextHour, value);
    }

    // 函数执行耗时
    private string _lastCallDuration;
    public string LastCallDuration
    {
        get => _lastCallDuration;
        set => this.RaiseAndSetIfChanged(ref _lastCallDuration, value);
    }

    // 自动测速状态显示
    private string _autoSpeedTestStatus;
    public string AutoSpeedTestStatus
    {
        get => _autoSpeedTestStatus;
        set => this.RaiseAndSetIfChanged(ref _autoSpeedTestStatus, value);
    }

    // 自动测速启用状态
    private bool _isAutoSpeedTestEnabled;
    public bool IsAutoSpeedTestEnabled
    {
        get => _isAutoSpeedTestEnabled;
        set => this.RaiseAndSetIfChanged(ref _isAutoSpeedTestEnabled, value);
    }
    #endregion Menu

    #region Init

    public ProfilesViewModel(Func<EViewAction, object?, Task<bool>>? updateView)
    {
        _config = AppManager.Instance.Config;
        _updateView = updateView;

        #region WhenAnyValue && ReactiveCommand

        var canEditRemove = this.WhenAnyValue(
           x => x.SelectedProfile,
           selectedSource => selectedSource != null && !selectedSource.IndexId.IsNullOrEmpty());

        this.WhenAnyValue(
            x => x.SelectedSub,
            y => y != null && !y.Remarks.IsNullOrEmpty() && _config.SubIndexId != y.Id)
                .Subscribe(async c => await SubSelectedChangedAsync(c));
        this.WhenAnyValue(
             x => x.SelectedMoveToGroup,
             y => y != null && !y.Remarks.IsNullOrEmpty())
                 .Subscribe(async c => await MoveToGroup(c));

        this.WhenAnyValue(
          x => x.ServerFilter,
          y => y != null && _serverFilter != y)
              .Subscribe(async c => await ServerFilterChanged(c));

        //servers delete
        EditServerCmd = ReactiveCommand.CreateFromTask(async () =>
        {
            await EditServerAsync();
        }, canEditRemove);
        RemoveServerCmd = ReactiveCommand.CreateFromTask(async () =>
        {
            await RemoveServerAsync();
        }, canEditRemove);
        RemoveDuplicateServerCmd = ReactiveCommand.CreateFromTask(async () =>
        {
            await RemoveDuplicateServer();
        });
        CopyServerCmd = ReactiveCommand.CreateFromTask(async () =>
        {
            await CopyServer();
        }, canEditRemove);
        SetDefaultServerCmd = ReactiveCommand.CreateFromTask(async () =>
        {
            await SetDefaultServer();
        }, canEditRemove);
        ShareServerCmd = ReactiveCommand.CreateFromTask(async () =>
        {
            await ShareServerAsync();
        }, canEditRemove);
        GenGroupMultipleServerXrayRandomCmd = ReactiveCommand.CreateFromTask(async () =>
        {
            await GenGroupMultipleServer(ECoreType.Xray, EMultipleLoad.Random);
        }, canEditRemove);
        GenGroupMultipleServerXrayRoundRobinCmd = ReactiveCommand.CreateFromTask(async () =>
        {
            await GenGroupMultipleServer(ECoreType.Xray, EMultipleLoad.RoundRobin);
        }, canEditRemove);
        GenGroupMultipleServerXrayLeastPingCmd = ReactiveCommand.CreateFromTask(async () =>
        {
            await GenGroupMultipleServer(ECoreType.Xray, EMultipleLoad.LeastPing);
        }, canEditRemove);
        GenGroupMultipleServerXrayLeastLoadCmd = ReactiveCommand.CreateFromTask(async () =>
        {
            await GenGroupMultipleServer(ECoreType.Xray, EMultipleLoad.LeastLoad);
        }, canEditRemove);
        GenGroupMultipleServerXrayFallbackCmd = ReactiveCommand.CreateFromTask(async () =>
        {
            await GenGroupMultipleServer(ECoreType.Xray, EMultipleLoad.Fallback);
        }, canEditRemove);
        GenGroupMultipleServerSingBoxLeastPingCmd = ReactiveCommand.CreateFromTask(async () =>
        {
            await GenGroupMultipleServer(ECoreType.sing_box, EMultipleLoad.LeastPing);
        }, canEditRemove);
        GenGroupMultipleServerSingBoxFallbackCmd = ReactiveCommand.CreateFromTask(async () =>
        {
            await GenGroupMultipleServer(ECoreType.sing_box, EMultipleLoad.Fallback);
        }, canEditRemove);

        //servers move
        MoveTopCmd = ReactiveCommand.CreateFromTask(async () =>
        {
            await MoveServer(EMove.Top);
        }, canEditRemove);
        MoveUpCmd = ReactiveCommand.CreateFromTask(async () =>
        {
            await MoveServer(EMove.Up);
        }, canEditRemove);
        MoveDownCmd = ReactiveCommand.CreateFromTask(async () =>
        {
            await MoveServer(EMove.Down);
        }, canEditRemove);
        MoveBottomCmd = ReactiveCommand.CreateFromTask(async () =>
        {
            await MoveServer(EMove.Bottom);
        }, canEditRemove);
        MoveToGroupCmd = ReactiveCommand.CreateFromTask<SubItem>(async sub =>
        {
            SelectedMoveToGroup = sub;
        });

        //servers ping
        AutoSpeedTestCmd = ReactiveCommand.CreateFromTask(async () =>
        {
            // fire-and-forget，明确放到后台线程
            _ = Task.Run(async () =>
            {
                try
                {
                    await TriggerOnTheTopOfHour(true);
                }
                catch (Exception ex)
                {
                    Logging.SaveLog("TriggerOnTheTopOfHour failed : " + ex.ToString());
                }
            });
        });
        FastRealPingCmd = ReactiveCommand.CreateFromTask(async () =>
        {
            await ServerSpeedtest(ESpeedActionType.FastRealping);
        });
        MixedTestServerCmd = ReactiveCommand.CreateFromTask(async () =>
        {
            await ServerSpeedtest(ESpeedActionType.Mixedtest);
        });
        TcpingServerCmd = ReactiveCommand.CreateFromTask(async () =>
        {
            await ServerSpeedtest(ESpeedActionType.Tcping);
        }, canEditRemove);
        RealPingServerCmd = ReactiveCommand.CreateFromTask(async () =>
        {
            await ServerSpeedtest(ESpeedActionType.Realping);
        }, canEditRemove);
        SpeedServerCmd = ReactiveCommand.CreateFromTask(async () =>
        {
            await ServerSpeedtest(ESpeedActionType.Speedtest);
        }, canEditRemove);
        SortServerResultCmd = ReactiveCommand.CreateFromTask(async () =>
        {
            await SortServer(EServerColName.DelayVal.ToString());
        });
        RemoveInvalidServerResultCmd = ReactiveCommand.CreateFromTask(async () =>
        {
            await RemoveInvalidServerResult();
        });
        //servers export
        Export2ClientConfigCmd = ReactiveCommand.CreateFromTask(async () =>
        {
            await Export2ClientConfigAsync(false);
        }, canEditRemove);
        Export2ClientConfigClipboardCmd = ReactiveCommand.CreateFromTask(async () =>
        {
            await Export2ClientConfigAsync(true);
        }, canEditRemove);
        Export2ShareUrlCmd = ReactiveCommand.CreateFromTask(async () =>
        {
            await Export2ShareUrlAsync(false);
        }, canEditRemove);
        Export2ShareUrlBase64Cmd = ReactiveCommand.CreateFromTask(async () =>
        {
            await Export2ShareUrlAsync(true);
        }, canEditRemove);

        //Subscription
        AddSubCmd = ReactiveCommand.CreateFromTask(async () =>
        {
            await EditSubAsync(true);
        });
        EditSubCmd = ReactiveCommand.CreateFromTask(async () =>
        {
            await EditSubAsync(false);
        });
        DeleteSubCmd = ReactiveCommand.CreateFromTask(async () =>
        {
            await DeleteSubAsync();
        });

        #endregion WhenAnyValue && ReactiveCommand

        #region AppEvents

        AppEvents.ProfilesRefreshRequested
            .AsObservable()
            .ObserveOn(RxApp.MainThreadScheduler)
            .Subscribe(async _ => await RefreshServersBiz());

        AppEvents.SubscriptionsRefreshRequested
            .AsObservable()
            .ObserveOn(RxApp.MainThreadScheduler)
            .Subscribe(async _ => await RefreshSubscriptions());

        AppEvents.DispatcherStatisticsRequested
            .AsObservable()
            .ObserveOn(RxApp.MainThreadScheduler)
            .Subscribe(async result => await UpdateStatistics(result));

        AppEvents.SetDefaultServerRequested
            .AsObservable()
            .ObserveOn(RxApp.MainThreadScheduler)
            .Subscribe(async indexId => await SetDefaultServer(indexId));

        #endregion AppEvents

        _ = Init();
    }

    private async Task Init()
    {
        SelectedProfile = new();
        SelectedSub = new();
        SelectedMoveToGroup = new();

        await RefreshSubscriptions();
        //await RefreshServers();

        StartTimer();
    }

    #endregion Init

    // ---------------------------------------------------------
    // 1) 启动定时器（每秒触发）
    // ---------------------------------------------------------
    private void StartTimer()
    {
        // 每秒触发一次
        Observable.Interval(TimeSpan.FromSeconds(1))
            .ObserveOn(RxApp.MainThreadScheduler)
            .Subscribe(_ =>
            {
                TimeToNextHour = GetTimeToNextHour();
            })
            .DisposeWith(_disposables);

        // 每秒检测是否到整点（不是整点不会触发）
        Observable.Interval(TimeSpan.FromSeconds(1))
            .Subscribe(async _ => await TriggerOnTheTopOfHour(false))
            .DisposeWith(_disposables);
    }

    // ---------------------------------------------------------
    // 2) 计算“距离下一个整点”的时间
    // ---------------------------------------------------------
    private static string GetTimeToNextHour()
    {
        var now = DateTime.Now;
        var nextHour = new DateTime(now.Year, now.Month, now.Day, now.Hour, 0, 0).AddHours(1);

        // 格式化为 HH:mm:ss（总是显示两位小时数，即使小于10小时）
        return (nextHour - now).ToString(@"hh\:mm\:ss");
    }

    // ---------------------------------------------------------
    // 3) 检测是否到整点，是整点就启动自动测速
    // ---------------------------------------------------------
    private async Task TriggerOnTheTopOfHour(bool isTriggeredManually)
    {
        if ((DateTime.Now.Minute == 0 && DateTime.Now.Second == 0) || (isTriggeredManually == true))    // Triggered by timer on the top of hour or triggered by manually hitting the button
        {
            string message;

            // 首先检查自动测速功能是否启用
            if (IsAutoSpeedTestEnabled == false)
            {
                message = $"AutoSpeedTest is not enabled. Speed test will not run at this time.";
                SaveLogAndSendMessageEx(message);

                await SetAutoSpeedTestStatus(message);

                return;
            }

            // 防止在运行自动测速的过程中，被再次触发
            if (isInAutoSpeedTestRound)
            {
                if (isTriggeredManually)
                {
                    message = "Manually triggered test while AutoSpeedTest is already running, ignore this trigger and waiting...";
                }
                else
                {
                    message = "Timer triggered test on the top of hour while AutoSpeedTest is already running, ignore this trigger and waiting...";
                }

                SaveLogAndSendMessageEx(message);

                return;
            }

            message = "AutoSpeedTest is enabled. Speed test begin to run...";
            SaveLogAndSendMessageEx(message);

            isInAutoSpeedTestRound = true;

            var sw = Stopwatch.StartNew();

            await AutoSpeedTest();

            sw.Stop();

            LastCallDuration = $"{sw.Elapsed}".Substring(0, 8);

            isInAutoSpeedTestRound = false;

            if (IsAutoSpeedTestEnabled)
            {
                message = "AutoSpeedTest is enabled. Speed test running done.";
            }
            else
            {
                message = "AutoSpeedTest was enabled, but been disabled while test running. This round of test been interrupted.";
            }

            message += " Running duration : " + LastCallDuration;
            SaveLogAndSendMessageEx(message);
        }
    }

    // ---------------------------------------------------------
    // 4) 自动测速主流程
    // ---------------------------------------------------------
    private async Task<Unit> AutoSpeedTest()
    {
        try
        {
            while (IsAutoSpeedTestEnabled)
            {
                var message = "================================================================================";
                SaveLogAndSendMessageEx(message);

                var sw = Stopwatch.StartNew();

                // 1. 执行一键多线程测试延迟和速度
                await SetAutoSpeedTestStatus("Step 1 of 10 : Running speed test.");
                await DoSpeedTest();

                // 2. 移除无效的 Server，两次 SpeedVal 为空，或者 为跳过测试 或者 速度为失败信息的 server 等不是 decimal 类型的
                await SetAutoSpeedTestStatus("Step 2 of 10 : Removing invalid servers.");
                await DoRemoveInvalidBySpeed();

                // 3. 按速度排序
                await SetAutoSpeedTestStatus("Step 3 of 10 : Sorting by speed test result.");
                await DoSortBySpeed();

                // 4. 选择最快服务器（特殊逻辑）
                await SetAutoSpeedTestStatus("Step 4 of 10 : Setting active server.");
                await DoSetServerAfterSpeedTesting();

                sw.Stop();

                var LastTestDuration = $"{sw.Elapsed}".Substring(0, 8);

                message = "--------------------------------------------------------------------------------";
                SaveLogAndSendMessageEx(message);

                // 半路检查是否停止自动测速
                if (IsAutoSpeedTestEnabled == false)
                {
                    message = "********************************************************************************";
                    SaveLogAndSendMessageEx(message);
                    message = "***** AutoSpeedTest disabled manually. Stop the current round of test now. *****";
                    SaveLogAndSendMessageEx(message);

                    await SetAutoSpeedTestStatus(message);

                    message = "********************************************************************************";
                    SaveLogAndSendMessageEx(message);

                    break; // 立即退出
                }

                // 当 ProfileItems 数量少于 20 ，或者 速度大于 5 的数量少于 5，则更新订阅，进行 delay 测试。如果不是，则等待 5 分钟后重复 1 - 4 步骤。
                var isNeedUpdate = await IsNeedUpdate();

                if (isNeedUpdate)
                {
                    message = $"Last round of speed test duration : {LastTestDuration}  Status is not good, going to update all subscriptions now.";
                    SaveLogAndSendMessageEx(message);

                    // 5. 更新全部订阅（通过代理）
                    await SetAutoSpeedTestStatus("Step 5 of 10 : Updating all subscriptions.");
                    await DoUpdateSubscription();

                    // 6. 移除重复
                    await SetAutoSpeedTestStatus("Step 6 of 10 : Removing duplicated server.");
                    await DoRemoveDuplication();

                    // 7. 执行一键测试真连接延迟
                    await SetAutoSpeedTestStatus("Step 7 of 10 : Running delay test.");
                    await DoDelayTest();

                    // 8. 移除无效的 Server
                    await SetAutoSpeedTestStatus("Step 8 of 10 : Removing invalid servers.");
                    await DoRemoveInvalidByDelay();

                    // 9. 按延迟排序
                    await SetAutoSpeedTestStatus("Step 9 of 10 : Sorting by delay test result.");
                    await DoSortByDelay();
                }
                else
                {
                    // 休息等待 5 分钟
                    //message += $"  Status is good, no need to update subscriptions, waiting for 5 minutes to run loop test of the top {currentItemLoopCount} servers.";
                    //SaveLogAndSendMessageEx(message);
                    //await SetAutoSpeedTestStatus(message);
                    //await WaitForFiveMinutes();

                    message = $"Last round of speed test duration : {LastTestDuration}  Status is good ! Started loop testing of the top {currentItemLoopCount} servers.";
                    SaveLogAndSendMessageEx(message);

                    // 10. 循环测试，速度最快的 10 个 Server
                    await SetAutoSpeedTestStatus($"Step 10 of 10 : Status is good ! Started loop testing of the top {currentItemLoopCount} servers.");
                    await DoTopTenLoopTest();
                }
            }
        }
        catch (Exception ex)
        {
            Logging.SaveLog($"{ex.Message}");
        }

        await Task.CompletedTask;

        return Unit.Default;
    }

    // ---------------------------------------------------------
    // 5) 释放资源
    // ---------------------------------------------------------
    public void Dispose()
    {
        _disposables.Dispose();
    }

    private async Task DoSpeedTest()
    {
        Logging.SaveLog("DoSpeedTest begin...");

        Logging.SaveLog("Stop the might running speed test first.");
        isSpeedTestRunning = false;
        ServerSpeedtestStop();

        Logging.SaveLog("Wait 10 seconds...");
        await Task.Delay(1000 * 10);

        if (IsAutoSpeedTestEnabled)
        {
            isSpeedTestRunning = true;
            await ServerSpeedtest(ESpeedActionType.Mixedtest);
        }

        while (isSpeedTestRunning)
        {
            var oldCount = ProfileItems.Count(item => item.SpeedVal == ResUI.SpeedtestingWait);

            Logging.SaveLog("DoSpeedTest is running, waiting for 1 minute...");

            await WaitForOneMinute();

            var newCount = ProfileItems.Count(item => item.SpeedVal == ResUI.SpeedtestingWait);

            Logging.SaveLog("DoSpeedTest SpeedtestingWait count before sleep : " + oldCount);
            Logging.SaveLog("DoSpeedTest SpeedtestingWait count  after sleep : " + newCount);

            if (newCount <= 0 || newCount == oldCount || IsAutoSpeedTestEnabled == false)
            {
                string message;
                if (IsAutoSpeedTestEnabled)
                {
                    message = "Current round of speed test done or no speed test is running during the 1 minute. Stop the current round of speed test now.";
                }
                else
                {
                    message = "AutoSpeedTest disabled manually. Stop the current round of speed test now.";
                }
                Logging.SaveLog(message);

                isSpeedTestRunning = false;
                ServerSpeedtestStop();

                Logging.SaveLog("Wait 10 seconds...");
                await Task.Delay(1000 * 10);
            }
            else
            {
                await DoSetServerWhileSpeedTesting();
            }
        }

        Logging.SaveLog("DoSpeedTest end.");
    }

    private async Task DoRemoveInvalidBySpeed()
    {
        Logging.SaveLog("DoRemoveInvalidBySpeed begin...");

        ProfileItemsFailedCurrent.Clear();
        ProfileItemsFailedCurrent.AddRange(ProfileItems.Where(item =>   item.SpeedVal.IsNullOrEmpty() ||
                                                                        item.SpeedVal == ResUI.SpeedtestingSkip ||
                                                                        decimal.TryParse(item.SpeedVal, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var valueResult) == false
                                                             ).ToList());

        var intersectionTwoTimes = ProfileItemsFailedCurrent.IntersectBy<ProfileItemModel, string>(ProfileItemsFailedLast.Select(it => it.IndexId), item => item.IndexId);

        var intersectionThreeTimes = intersectionTwoTimes.IntersectBy<ProfileItemModel, string>(ProfileItemsFailedFirst.Select(it => it.IndexId), item => item.IndexId);

        Logging.SaveLog("ProfileItemsFailedFirst.Count      : " + ProfileItemsFailedFirst.Count);
        Logging.SaveLog("ProfileItemsFailedLast.Count       : " + ProfileItemsFailedLast.Count);
        Logging.SaveLog("ProfileItemsFailedCurrent.Count    : " + ProfileItemsFailedCurrent.Count);
        Logging.SaveLog("ProfileItemsFailedThreeTimes.Count : " + intersectionThreeTimes.Count());

        var temp = ProfileItemsFailedFirst;
        ProfileItemsFailedFirst = ProfileItemsFailedLast;
        ProfileItemsFailedLast = ProfileItemsFailedCurrent;
        ProfileItemsFailedCurrent = temp;

        var oldCount = ProfileItems.Count;

        SelectedProfiles = intersectionThreeTimes.ToList();

        var lstSelected = await GetProfileItems(true);
        if (lstSelected != null && lstSelected.Count > 0)
        {
            var exists = lstSelected.Exists(t => t.IndexId == _config.IndexId);

            await ConfigHandler.RemoveServers(_config, lstSelected);

            var message = ResUI.OperationSuccess;
            SaveLogAndSendMessageEx(message);

            if (lstSelected.Count == ProfileItems.Count)
            {
                ProfileItems.Clear();
            }
            await RefreshServers();
            if (exists)
            {
                Reload();
            }
        }

        Logging.SaveLog("Wait 10 seconds...");
        await Task.Delay(1000 * 10);

        var newCount = ProfileItems.Count;

        Logging.SaveLog("ProfileItems.Count before invalid removing by speed : " + oldCount);
        Logging.SaveLog("ProfileItems.Count  after invalid removing by speed : " + newCount);

        Logging.SaveLog("DoRemoveInvalidBySpeed end.");
    }

    private async Task DoSortBySpeed()
    {
        Logging.SaveLog("DoSortBySpeed begin...");

        await SortServer(EServerColName.SpeedVal.ToString());

        Logging.SaveLog("Wait 2 seconds...");
        await Task.Delay(1000 * 2);

        if (ProfileItems.Count > 1)
        {
            var firstSpeed = ProfileItems[0].Speed;
            decimal nextSpeed;

            var index = 1;
            do
            {
                nextSpeed = ProfileItems[index].Speed;
                index++;
            } while (index < ProfileItems.Count && firstSpeed.Equals(nextSpeed));

            if (firstSpeed < nextSpeed)
            {
                await SortServer(EServerColName.SpeedVal.ToString());

                Logging.SaveLog("Wait 2 seconds...");
                await Task.Delay(1000 * 2);
            }
        }

        Logging.SaveLog("DoSortBySpeed end.");
    }

    private async Task DoSetServerWhileSpeedTesting()
    {
        Logging.SaveLog("DoSetServerWhileSpeedTesting begin...");

        if (ProfileItems != null && ProfileItems.Count > 0)
        {
            // 在测速过程中，
            var selected = ProfileItems.FirstOrDefault(item => item.Delay is > 0 and < 500 && item.Speed > 1 && item.Remarks.IsNotEmpty() && (item.Remarks.ToLower().Contains("us") || item.Remarks.Contains("美国")));
            selected ??= ProfileItems.FirstOrDefault(item => item.Delay is > 0 and < 500 && item.Speed > 30);
            selected ??= ProfileItems.FirstOrDefault(item => item.Delay is > 0 and < 500 && item.Speed > 10);
            selected ??= ProfileItems.FirstOrDefault(item => item.Delay is > 0 and < 500 && item.Speed > 5);
            selected ??= ProfileItems.FirstOrDefault(item => item.Delay is > 0 and < 500 && item.Speed > 1);
            selected ??= ProfileItems.FirstOrDefault(item => item.Speed > 30);
            selected ??= ProfileItems.FirstOrDefault(item => item.Speed > 10);
            selected ??= ProfileItems.FirstOrDefault(item => item.Speed > 5);
            selected ??= ProfileItems.FirstOrDefault(item => item.Speed > 1);
            selected ??= ProfileItems.FirstOrDefault(item => item.Speed > 0);

            await DoSetServer(selected);
        }

        Logging.SaveLog("DoSetServerWhileSpeedTesting end.");
    }

    private async Task DoSetServerAfterSpeedTesting()
    {
        Logging.SaveLog("DoSetServerAfterSpeedTesting begin...");

        if (ProfileItems != null && ProfileItems.Count > 0)
        {
            // 已经按照测试速度的结果，按速度值去除无效的 server，所以 item.Speed 一定是有效的 decimal 数值。
            // 已经按照测试速度的结果，按速度值从大到小排列，所以 ProfileItems[0] 的速度值一定是最大的，但是其 item.Delay 值可能不是在 0 到 500 区间。
            var selected = ProfileItems.FirstOrDefault(item => item.Delay is > 0 and < 500 && item.Speed > 1 && item.Remarks.IsNotEmpty() && (item.Remarks.ToLower().Contains("us") || item.Remarks.Contains("美国")));
            selected ??= ProfileItems.FirstOrDefault(item => item.Delay is > 0 and < 500 && item.Speed > 1);
            selected ??= ProfileItems[0];

            await DoSetServer(selected);
        }

        Logging.SaveLog("DoSetServerAfterSpeedTesting end.");
    }

    private async Task DoSetServer(ProfileItemModel selected)
    {
        if (selected != null)
        {
            // Assign SelectedProfile on the main/UI thread to avoid cross-thread access exceptions
            RxApp.MainThreadScheduler.Schedule(selected, (scheduler, model) =>
            {
                SelectedProfile = model;
                return Disposable.Empty;
            });

            // Use the selected item's IndexId when setting default server to avoid reading SelectedProfile from a background thread
            await SetDefaultServer(selected.IndexId);
            Logging.SaveLog("Wait 2 second...");
            await Task.Delay(1000 * 2);
        }
    }

    private async Task<bool> IsNeedUpdate()
    {
        // 速度大于 5 的 server 总数
        var validSpeedProfileCount = ProfileItems.Count(item => item.Speed > minValidSpeed);

        // 保存本次小循环测试的 server 数，如果超过 10，就设置为 10
        currentItemLoopCount = Math.Min(validSpeedProfileCount, maxItemLoopCount);

        // ProfileItems 总数小于 20 
        if (ProfileItems.Count < minValidProfileCount)
        {
            Logging.SaveLog($"All the profiles count now is {ProfileItems.Count} < {minValidProfileCount} , need to update subscription.");
            return true;
        }

        // 在 ProfileItems 里统计速度大于 5 的 server 的总数 小于 3
        if (validSpeedProfileCount < minValidSpeedProfileCount)
        {
            Logging.SaveLog($"Speed value bigger than {minValidSpeed} profiles count is {validSpeedProfileCount} < {minValidSpeedProfileCount} , need to update subscription.");
            return true;
        }

        return false;
    }

    private async Task DoUpdateSubscription()
    {
        Logging.SaveLog("DoUpdateSubscription begin...");

        await Task.Run(async () => await SubscriptionHandler.UpdateProcess(_config, "", true, UpdateTaskHandler));

        Logging.SaveLog("Wait 2 seconds...");
        await Task.Delay(1000 * 2);

        Logging.SaveLog("DoUpdateSubscription end.");
    }

    private async Task UpdateTaskHandler(bool success, string message)
    {
        SaveLogAndSendMessageEx(message);

        if (success)
        {
            var indexIdOld = _config.IndexId;
            await RefreshServers();
            if (indexIdOld != _config.IndexId)
            {
                Reload();
            }
            if (_config.UiItem.EnableAutoAdjustMainLvColWidth)
            {
                AppEvents.AdjustMainLvColWidthRequested.Publish();
            }
        }
    }

    private async Task DoRemoveDuplication()
    {
        Logging.SaveLog("DoRemoveDuplication begin...");

        var oldCount = ProfileItems.Count;

        var tuple = await ConfigHandler.DedupServerList(_config, _config.SubIndexId);
        if (tuple.Item1 > 0 || tuple.Item2 > 0)
        {
            await RefreshServers();
            Reload();
        }
        var message = string.Format(ResUI.RemoveDuplicateServerResult, tuple.Item1, tuple.Item2);
        SaveLogAndSendMessageEx(message);

        Logging.SaveLog("Wait 2 seconds...");
        await Task.Delay(1000 * 2);

        var newCount = ProfileItems.Count;

        Logging.SaveLog("ProfileItems.Count before removing duplication : " + oldCount);
        Logging.SaveLog("ProfileItems.Count  after removing duplication : " + newCount);

        Logging.SaveLog("DoRemoveDuplication end.");
    }

    private async Task DoDelayTest()
    {
        Logging.SaveLog("DoDelayTest begin...");

        Logging.SaveLog("Stop the might running delay test first.");
        isDelayTestRunning = false;
        ServerSpeedtestStop();

        Logging.SaveLog("Wait 10 seconds...");
        await Task.Delay(1000 * 10);

        if (IsAutoSpeedTestEnabled)
        {
            isDelayTestRunning = true;
            await ServerSpeedtest(ESpeedActionType.FastRealping);
        }

        while (isDelayTestRunning)
        {
            var oldCount = ProfileItems.Count(item => item.DelayVal == ResUI.Speedtesting);

            Logging.SaveLog("DoDelayTest is running, waiting for 1 minute...");

            await WaitForOneMinute();

            var newCount = ProfileItems.Count(item => item.DelayVal == ResUI.Speedtesting);

            Logging.SaveLog("DoDelayTest Speedtesting count before sleep : " + oldCount);
            Logging.SaveLog("DoDelayTest Speedtesting count  after sleep : " + newCount);

            if (newCount <= 0 || newCount == oldCount || IsAutoSpeedTestEnabled == false)
            {
                string message;
                if (IsAutoSpeedTestEnabled)
                {
                    message = "Current round of delay test done or no delay test is running during the 1 minute. Stop the current round of delay test now.";
                }
                else
                {
                    message = "AutoSpeedTest disabled manually. Stop the current round of delay test now.";
                }
                Logging.SaveLog(message);

                isDelayTestRunning = false;
                ServerSpeedtestStop();

                Logging.SaveLog("Wait 10 seconds...");
                await Task.Delay(1000 * 10);
            }
        }

        Logging.SaveLog("DoDelayTest end.");
    }

    private async Task DoRemoveInvalidByDelay()
    {
        Logging.SaveLog("DoRemoveInvalidByDelay begin...");

        // 把无效配置的 Server （它们的 delay value 显示为空白）的 delay 设置为 -1 ，等待着和有效配置但是 delay 测试无效的 Server 一起移除掉。
        ProfileItems.Where(item => item.DelayVal.IsNullOrEmpty()).ToList().ForEach(item => ProfileExManager.Instance.SetTestDelay(item.IndexId, -1));

        var oldCount = ProfileItems.Count;

        await RemoveInvalidServerResult();

        Logging.SaveLog("Wait 10 seconds...");
        await Task.Delay(1000 * 10);

        var newCount = ProfileItems.Count;

        Logging.SaveLog("ProfileItems.Count before invalid removing by delay : " + oldCount);
        Logging.SaveLog("ProfileItems.Count  after invalid removing by delay : " + newCount);

        Logging.SaveLog("DoRemoveInvalidByDelay end.");
    }

    private async Task DoSortByDelay()
    {
        Logging.SaveLog("DoSortByDelay begin...");

        await SortServer(EServerColName.DelayVal.ToString());

        Logging.SaveLog("Wait 2 seconds...");
        await Task.Delay(1000 * 2);

        if (ProfileItems.Count > 1)
        {
            var firstDelay = ProfileItems[0].Delay;
            int nextDelay;

            var index = 1;
            do
            {
                nextDelay = ProfileItems[index].Delay;
                index++;
            } while (index < ProfileItems.Count && firstDelay.Equals(nextDelay));

            if (firstDelay > nextDelay)
            {
                await SortServer(EServerColName.DelayVal.ToString());

                Logging.SaveLog("Wait 2 seconds...");
                await Task.Delay(1000 * 2);
            }
        }

        Logging.SaveLog("DoSortByDelay end.");
    }

    private async Task DoTopTenLoopTest()
    {
        Logging.SaveLog("DoTopTenLoopTest begin...");

        // 循环对前 10 个服务器进行定时测速，根据测速结果，判断是否要继续循环还是再跳回到对所有 server 进行一键测试速度

        IList <ProfileItemModel> validSpeedProfileItems = [];
        for (var i = 0; i < minValidSpeedProfileCount; i++)
        {
            validSpeedProfileItems.Add(new ProfileItemModel());
        }

        while (IsAutoSpeedTestEnabled && validSpeedProfileItems.Count >= minValidSpeedProfileCount)
        {
            validSpeedProfileItems.Clear();

            for (var i = 0; IsAutoSpeedTestEnabled && i < currentItemLoopCount; i++)
            {
                var message = $"Testing the top {currentItemLoopCount} servers... Server No. {i + 1}";
                SaveLogAndSendMessageEx(message);

                var selected = ProfileItems[i];

                SelectedProfiles.Clear();
                SelectedProfiles.Add(selected);

                await ServerSpeedtest(ESpeedActionType.Speedtest);

                message = $"Wait 1 minute...";
                SaveLogAndSendMessageEx(message);

                await WaitForOneMinute();

                Logging.SaveLog("Stop the might running speed test.");

                ServerSpeedtestStop();

                Logging.SaveLog("Wait 10 seconds...");
                await Task.Delay(1000 * 10);

                // 如果 server 的速度大于 5 ，则存下来。
                if (selected.Speed > minValidSpeed)
                {
                    validSpeedProfileItems.Add(selected);
                }
            }

            if (validSpeedProfileItems.Count < minValidSpeedProfileCount)
            {
                // 如果 server 数量小于 3 ，则什么也不做，等待外层循环退出。
                continue;
            }
            else if (validSpeedProfileItems.Any(item => item.IndexId == _config.IndexId))
            {
                // 或者 如果 server 数量大于 3 ，但是当前的活动 server 就在这个 server 列表里面，也什么都不做，等待下一次循环。就是，保持当前的活动 server 不改变，保持网络的稳定性。
                continue;
            }
            else
            {
                // 如果 Server 数量大于 3 ，但是当前的活动 server 不在这个 server 列表里面，则重新对所有 server 进行按速度排序，然后重新设置活动 server。
                await DoSortBySpeed();
                await DoSetServerAfterSpeedTesting();
            }
        }

        Logging.SaveLog("DoTopTenLoopTest end.");
    }

    public async Task SetAutoSpeedTestStatus(string status)
    {
        if (status.IsNullOrEmpty())
        {
            return;
        }

        // Ensure we update the UI-bound property on the main/UI thread
        RxApp.MainThreadScheduler.Schedule(status, (scheduler, s) =>
        {
            try
            {
                AutoSpeedTestStatus = s;
            }
            catch (Exception ex)
            {
                Logging.SaveLog("Failed to set AutoSpeedTestStatus on UI thread", ex);
            }
            return Disposable.Empty;
        });

        await Task.CompletedTask;
    }

    private async Task WaitForFiveMinutes()
    {
        Logging.SaveLog("WaitForFiveMinutes begin...");

        var minuteCount = 0;
        while (IsAutoSpeedTestEnabled && minuteCount++ < 5)
        {
            var message = $"Wait 1 minute... (Iteration {minuteCount})";
            SaveLogAndSendMessageEx(message);

            await WaitForOneMinute();
        }

        Logging.SaveLog("WaitForFiveMinutes end.");

    }

    private async Task WaitForOneMinute()
    {
        var count = 0;
        while (IsAutoSpeedTestEnabled && count++ < 6)
        {
            //Logging.SaveLog($"Wait 10 seconds... (Iteration {count})");

            // 将10秒拆分为10个1秒的等待，每秒检查一次条件
            for (var i = 0; i < 10; i++)
            {
                await Task.Delay(1000); // 每次只等1秒

                // 每秒检查一次，响应更及时
                if (IsAutoSpeedTestEnabled == false)
                {
                    var message = "AutoSpeedTest disabled manually. Exiting early.";
                    SaveLogAndSendMessageEx(message);

                    break; // 立即退出
                }
            }
        }
    }

    private static void SaveLogAndSendMessageEx(string message)
    {
        NoticeManager.Instance.SendMessageEx(message);
        Logging.SaveLog(message);
    }
    #region Actions

    private void Reload()
    {
        AppEvents.ReloadRequested.Publish();
    }

    public async Task SetSpeedTestResult(SpeedTestResult result)
    {
        if (result.IndexId.IsNullOrEmpty())
        {
            NoticeManager.Instance.SendMessageEx(result.Delay);
            NoticeManager.Instance.Enqueue(result.Delay);
            return;
        }
        var item = ProfileItems.FirstOrDefault(it => it.IndexId == result.IndexId);
        if (item == null)
        {
            return;
        }

        if (result.Delay.IsNotEmpty())
        {
            item.Delay = result.Delay.ToInt();
            item.DelayVal = result.Delay ?? string.Empty;
        }
        if (result.Speed.IsNotEmpty())
        {
            item.Speed = decimal.TryParse(result.Speed, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var valueResult) ? valueResult : 0;
            item.SpeedVal = result.Speed ?? string.Empty;
        }
        await Task.CompletedTask;
    }

    public async Task UpdateStatistics(ServerSpeedItem update)
    {
        if (!_config.GuiItem.EnableStatistics
            || (update.ProxyUp + update.ProxyDown) <= 0
            || DateTime.Now.Second % 3 != 0)
        {
            return;
        }

        try
        {
            var item = ProfileItems.FirstOrDefault(it => it.IndexId == update.IndexId);
            if (item != null)
            {
                item.TodayDown = Utils.HumanFy(update.TodayDown);
                item.TodayUp = Utils.HumanFy(update.TodayUp);
                item.TotalDown = Utils.HumanFy(update.TotalDown);
                item.TotalUp = Utils.HumanFy(update.TotalUp);
            }
        }
        catch
        {
        }
        await Task.CompletedTask;
    }

    #endregion Actions

    #region Servers && Groups

    private async Task SubSelectedChangedAsync(bool c)
    {
        if (!c)
        {
            return;
        }
        _config.SubIndexId = SelectedSub?.Id;

        await RefreshServers();

        await _updateView?.Invoke(EViewAction.ProfilesFocus, null);
    }

    private async Task ServerFilterChanged(bool c)
    {
        if (!c)
        {
            return;
        }
        _serverFilter = ServerFilter;
        if (_serverFilter.IsNullOrEmpty())
        {
            await RefreshServers();
        }
    }

    public async Task RefreshServers()
    {
        AppEvents.ProfilesRefreshRequested.Publish();

        await Task.Delay(200);
    }

    private async Task RefreshServersBiz()
    {
        var lstModel = await GetProfileItemsEx(_config.SubIndexId, _serverFilter);
        _lstProfile = JsonUtils.Deserialize<List<ProfileItem>>(JsonUtils.Serialize(lstModel)) ?? [];

        ProfileItems.Clear();
        ProfileItems.AddRange(lstModel);
        if (lstModel.Count > 0)
        {
            var selected = lstModel.FirstOrDefault(t => t.IndexId == _config.IndexId);
            if (selected != null)
            {
                SelectedProfile = selected;
            }
            else
            {
                SelectedProfile = lstModel.First();
            }
        }

        await _updateView?.Invoke(EViewAction.DispatcherRefreshServersBiz, null);
    }

    private async Task RefreshSubscriptions()
    {
        SubItems.Clear();

        SubItems.Add(new SubItem { Remarks = ResUI.AllGroupServers });

        foreach (var item in await AppManager.Instance.SubItems())
        {
            SubItems.Add(item);
        }
        if (_config.SubIndexId != null && SubItems.FirstOrDefault(t => t.Id == _config.SubIndexId) != null)
        {
            SelectedSub = SubItems.FirstOrDefault(t => t.Id == _config.SubIndexId);
        }
        else
        {
            SelectedSub = SubItems.First();
        }
    }

    private async Task<List<ProfileItemModel>?> GetProfileItemsEx(string subid, string filter)
    {
        var lstModel = await AppManager.Instance.ProfileItems(_config.SubIndexId, filter);

        await ConfigHandler.SetDefaultServer(_config, lstModel);

        var lstServerStat = (_config.GuiItem.EnableStatistics ? StatisticsManager.Instance.ServerStat : null) ?? [];
        var lstProfileExs = await ProfileExManager.Instance.GetProfileExs();
        lstModel = (from t in lstModel
                    join t2 in lstServerStat on t.IndexId equals t2.IndexId into t2b
                    from t22 in t2b.DefaultIfEmpty()
                    join t3 in lstProfileExs on t.IndexId equals t3.IndexId into t3b
                    from t33 in t3b.DefaultIfEmpty()
                    select new ProfileItemModel
                    {
                        IndexId = t.IndexId,
                        ConfigType = t.ConfigType,
                        Remarks = t.Remarks,
                        Address = t.Address,
                        Port = t.Port,
                        Security = t.Security,
                        Network = t.Network,
                        StreamSecurity = t.StreamSecurity,
                        Subid = t.Subid,
                        SubRemarks = t.SubRemarks,
                        IsActive = t.IndexId == _config.IndexId,
                        Sort = t33?.Sort ?? 0,
                        Delay = t33?.Delay ?? 0,
                        Speed = t33?.Speed ?? 0,
                        DelayVal = t33?.Delay != 0 ? $"{t33?.Delay}" : string.Empty,
                        SpeedVal = t33?.Speed > 0 ? $"{t33?.Speed}" : t33?.Message ?? string.Empty,
                        TodayDown = t22 == null ? "" : Utils.HumanFy(t22.TodayDown),
                        TodayUp = t22 == null ? "" : Utils.HumanFy(t22.TodayUp),
                        TotalDown = t22 == null ? "" : Utils.HumanFy(t22.TotalDown),
                        TotalUp = t22 == null ? "" : Utils.HumanFy(t22.TotalUp)
                    }).OrderBy(t => t.Sort).ToList();

        return lstModel;
    }

    #endregion Servers && Groups

    #region Add Servers

    private async Task<List<ProfileItem>?> GetProfileItems(bool latest)
    {
        var lstSelected = new List<ProfileItem>();
        if (SelectedProfiles == null || SelectedProfiles.Count <= 0)
        {
            return null;
        }

        var orderProfiles = SelectedProfiles?.OrderBy(t => t.Sort);
        if (latest)
        {
            foreach (var profile in orderProfiles)
            {
                var item = await AppManager.Instance.GetProfileItem(profile.IndexId);
                if (item is not null)
                {
                    lstSelected.Add(item);
                }
            }
        }
        else
        {
            lstSelected = JsonUtils.Deserialize<List<ProfileItem>>(JsonUtils.Serialize(orderProfiles));
        }

        return lstSelected;
    }

    public async Task EditServerAsync()
    {
        if (string.IsNullOrEmpty(SelectedProfile?.IndexId))
        {
            return;
        }
        var item = await AppManager.Instance.GetProfileItem(SelectedProfile.IndexId);
        if (item is null)
        {
            NoticeManager.Instance.Enqueue(ResUI.PleaseSelectServer);
            return;
        }
        var eConfigType = item.ConfigType;

        bool? ret = false;
        if (eConfigType == EConfigType.Custom)
        {
            ret = await _updateView?.Invoke(EViewAction.AddServer2Window, item);
        }
        else if (eConfigType.IsGroupType())
        {
            ret = await _updateView?.Invoke(EViewAction.AddGroupServerWindow, item);
        }
        else
        {
            ret = await _updateView?.Invoke(EViewAction.AddServerWindow, item);
        }
        if (ret == true)
        {
            await RefreshServers();
            if (item.IndexId == _config.IndexId)
            {
                Reload();
            }
        }
    }

    public async Task RemoveServerAsync()
    {
        var lstSelected = await GetProfileItems(true);
        if (lstSelected == null)
        {
            return;
        }
        if (await _updateView?.Invoke(EViewAction.ShowYesNo, null) == false)
        {
            return;
        }
        var exists = lstSelected.Exists(t => t.IndexId == _config.IndexId);

        await ConfigHandler.RemoveServers(_config, lstSelected);
        NoticeManager.Instance.Enqueue(ResUI.OperationSuccess);
        if (lstSelected.Count == ProfileItems.Count)
        {
            ProfileItems.Clear();
        }
        await RefreshServers();
        if (exists)
        {
            Reload();
        }
    }

    private async Task RemoveDuplicateServer()
    {
        if (await _updateView?.Invoke(EViewAction.ShowYesNo, null) == false)
        {
            return;
        }

        var tuple = await ConfigHandler.DedupServerList(_config, _config.SubIndexId);
        if (tuple.Item1 > 0 || tuple.Item2 > 0)
        {
            await RefreshServers();
            Reload();
        }
        NoticeManager.Instance.Enqueue(string.Format(ResUI.RemoveDuplicateServerResult, tuple.Item1, tuple.Item2));
    }

    private async Task CopyServer()
    {
        var lstSelected = await GetProfileItems(false);
        if (lstSelected == null)
        {
            return;
        }
        if (await ConfigHandler.CopyServer(_config, lstSelected) == 0)
        {
            await RefreshServers();
            NoticeManager.Instance.Enqueue(ResUI.OperationSuccess);
        }
    }

    public async Task SetDefaultServer()
    {
        if (string.IsNullOrEmpty(SelectedProfile?.IndexId))
        {
            return;
        }
        await SetDefaultServer(SelectedProfile.IndexId);
    }

    private async Task SetDefaultServer(string? indexId)
    {
        if (indexId.IsNullOrEmpty())
        {
            return;
        }
        if (indexId == _config.IndexId)
        {
            return;
        }
        var item = await AppManager.Instance.GetProfileItem(indexId);
        if (item is null)
        {
            NoticeManager.Instance.Enqueue(ResUI.PleaseSelectServer);
            return;
        }

        if (await ConfigHandler.SetDefaultServerIndex(_config, indexId) == 0)
        {
            await RefreshServers();
            Reload();
        }
    }

    public async Task ShareServerAsync()
    {
        var item = await AppManager.Instance.GetProfileItem(SelectedProfile.IndexId);
        if (item is null)
        {
            NoticeManager.Instance.Enqueue(ResUI.PleaseSelectServer);
            return;
        }
        var url = FmtHandler.GetShareUri(item);
        if (url.IsNullOrEmpty())
        {
            return;
        }

        await _updateView?.Invoke(EViewAction.ShareServer, url);
    }

    private async Task GenGroupMultipleServer(ECoreType coreType, EMultipleLoad multipleLoad)
    {
        var lstSelected = await GetProfileItems(true);
        if (lstSelected == null)
        {
            return;
        }

        var ret = await ConfigHandler.AddGroupServer4Multiple(_config, lstSelected, coreType, multipleLoad, SelectedSub?.Id);
        if (ret.Success != true)
        {
            NoticeManager.Instance.Enqueue(ResUI.OperationFailed);
            return;
        }
        if (ret?.Data?.ToString() == _config.IndexId)
        {
            await RefreshServers();
            Reload();
        }
        else
        {
            await SetDefaultServer(ret?.Data?.ToString());
        }
    }

    public async Task SortServer(string colName)
    {
        if (colName.IsNullOrEmpty())
        {
            return;
        }

        _dicHeaderSort.TryAdd(colName, true);
        _dicHeaderSort.TryGetValue(colName, out var asc);
        if (await ConfigHandler.SortServers(_config, _config.SubIndexId, colName, asc) != 0)
        {
            return;
        }
        _dicHeaderSort[colName] = !asc;
        await RefreshServers();
    }

    public async Task RemoveInvalidServerResult()
    {
        var count = await ConfigHandler.RemoveInvalidServerResult(_config, _config.SubIndexId);
        await RefreshServers();
        NoticeManager.Instance.Enqueue(string.Format(ResUI.RemoveInvalidServerResultTip, count));
    }

    //move server
    private async Task MoveToGroup(bool c)
    {
        if (!c)
        {
            return;
        }

        var lstSelected = await GetProfileItems(true);
        if (lstSelected == null)
        {
            return;
        }

        await ConfigHandler.MoveToGroup(_config, lstSelected, SelectedMoveToGroup.Id);
        NoticeManager.Instance.Enqueue(ResUI.OperationSuccess);

        await RefreshServers();
        SelectedMoveToGroup = null;
        SelectedMoveToGroup = new();
    }

    public async Task MoveServer(EMove eMove)
    {
        var item = _lstProfile.FirstOrDefault(t => t.IndexId == SelectedProfile.IndexId);
        if (item is null)
        {
            NoticeManager.Instance.Enqueue(ResUI.PleaseSelectServer);
            return;
        }

        var index = _lstProfile.IndexOf(item);
        if (index < 0)
        {
            return;
        }
        if (await ConfigHandler.MoveServer(_config, _lstProfile, index, eMove) == 0)
        {
            await RefreshServers();
        }
    }

    public async Task MoveServerTo(int startIndex, ProfileItemModel targetItem)
    {
        var targetIndex = ProfileItems.IndexOf(targetItem);
        if (startIndex >= 0 && targetIndex >= 0 && startIndex != targetIndex)
        {
            if (await ConfigHandler.MoveServer(_config, _lstProfile, startIndex, EMove.Position, targetIndex) == 0)
            {
                await RefreshServers();
            }
        }
    }

    public async Task ServerSpeedtest(ESpeedActionType actionType)
    {
        if (actionType == ESpeedActionType.Mixedtest)
        {
            SelectedProfiles = ProfileItems;
        }
        else if (actionType == ESpeedActionType.FastRealping)
        {
            SelectedProfiles = ProfileItems;
            actionType = ESpeedActionType.Realping;
        }

        var lstSelected = await GetProfileItems(false);
        if (lstSelected == null)
        {
            return;
        }

        _speedtestService ??= new SpeedtestService(_config, async (SpeedTestResult result) =>
        {
            RxApp.MainThreadScheduler.Schedule(result, (scheduler, result) =>
            {
                _ = SetSpeedTestResult(result);
                return Disposable.Empty;
            });
            await Task.CompletedTask;
        });
        _speedtestService?.RunLoop(actionType, lstSelected);
    }

    public void ServerSpeedtestStop()
    {
        _speedtestService?.ExitLoop();
    }

    private async Task Export2ClientConfigAsync(bool blClipboard)
    {
        var item = await AppManager.Instance.GetProfileItem(SelectedProfile.IndexId);
        if (item is null)
        {
            NoticeManager.Instance.Enqueue(ResUI.PleaseSelectServer);
            return;
        }

        var msgs = await ActionPrecheckManager.Instance.Check(item);
        if (msgs.Count > 0)
        {
            foreach (var msg in msgs)
            {
                NoticeManager.Instance.SendMessage(msg);
            }
            NoticeManager.Instance.Enqueue(Utils.List2String(msgs.Take(10).ToList(), true));
            return;
        }

        if (blClipboard)
        {
            var result = await CoreConfigHandler.GenerateClientConfig(item, null);
            if (result.Success != true)
            {
                NoticeManager.Instance.Enqueue(result.Msg);
            }
            else
            {
                await _updateView?.Invoke(EViewAction.SetClipboardData, result.Data);
                NoticeManager.Instance.SendMessage(ResUI.OperationSuccess);
            }
        }
        else
        {
            await _updateView?.Invoke(EViewAction.SaveFileDialog, item);
        }
    }

    public async Task Export2ClientConfigResult(string fileName, ProfileItem item)
    {
        if (fileName.IsNullOrEmpty())
        {
            return;
        }
        var result = await CoreConfigHandler.GenerateClientConfig(item, fileName);
        if (result.Success != true)
        {
            NoticeManager.Instance.Enqueue(result.Msg);
        }
        else
        {
            NoticeManager.Instance.SendMessageAndEnqueue(string.Format(ResUI.SaveClientConfigurationIn, fileName));
        }
    }

    public async Task Export2ShareUrlAsync(bool blEncode)
    {
        var lstSelected = await GetProfileItems(true);
        if (lstSelected == null)
        {
            return;
        }

        StringBuilder sb = new();
        foreach (var it in lstSelected)
        {
            var url = FmtHandler.GetShareUri(it);
            if (url.IsNullOrEmpty())
            {
                continue;
            }
            sb.Append(url);
            sb.AppendLine();
        }
        if (sb.Length > 0)
        {
            if (blEncode)
            {
                await _updateView?.Invoke(EViewAction.SetClipboardData, Utils.Base64Encode(sb.ToString()));
            }
            else
            {
                await _updateView?.Invoke(EViewAction.SetClipboardData, sb.ToString());
            }
            NoticeManager.Instance.SendMessage(ResUI.BatchExportURLSuccessfully);
        }
    }

    #endregion Add Servers

    #region Subscription

    private async Task EditSubAsync(bool blNew)
    {
        SubItem item;
        if (blNew)
        {
            item = new();
        }
        else
        {
            item = await AppManager.Instance.GetSubItem(_config.SubIndexId);
            if (item is null)
            {
                return;
            }
        }
        if (await _updateView?.Invoke(EViewAction.SubEditWindow, item) == true)
        {
            await RefreshSubscriptions();
            await SubSelectedChangedAsync(true);
        }
    }

    private async Task DeleteSubAsync()
    {
        var item = await AppManager.Instance.GetSubItem(_config.SubIndexId);
        if (item is null)
        {
            return;
        }

        if (await _updateView?.Invoke(EViewAction.ShowYesNo, null) == false)
        {
            return;
        }
        await ConfigHandler.DeleteSubItem(_config, item.Id);

        await RefreshSubscriptions();
        await SubSelectedChangedAsync(true);
    }

    #endregion Subscription
}
