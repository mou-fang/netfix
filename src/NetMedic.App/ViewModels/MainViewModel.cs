using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NetMedic.App.Resources;
using NetMedic.App.Windows;
using NetMedic.App.Windows.Probes;
using NetMedic.Core.Diagnostics;
using NetMedic.Core.Diagnostics.Rules;
using NetMedic.Core.Repairs;
using NetMedic.Core.Testing.Fakes;

namespace NetMedic.App;

/// <summary>
/// 应用页面状态。对应任务书 §4.1 单窗口四态切换。
/// </summary>
public enum AppPage
{
    Home,
    Checking,
    Result,
    RepairConfirm,
    RepairResult,
    MockRepair,
}

/// <summary>
/// 主视图模型。管理四页假流程的状态切换和诊断编排。
/// 阶段 1 使用 FakeNetworkEnvironment，不调用真实 Windows API。
/// </summary>
public sealed partial class MainViewModel : ObservableObject
{
    private CancellationTokenSource? _checkupCts;

    public MainViewModel()
    {
        this.appName = Strings.AppTitle;
        this.appVersion = "v0.2.0";
        this.readyState = Strings.Ready;
        this.currentPage = AppPage.Home;
        this.selectedSymptom = SymptomCategory.Unsure;
        this.progressText = string.Empty;
        this.currentStageText = string.Empty;
        this.primaryFinding = null;
        this.allFindings = [];
        this.selectedRepairAction = null;
        this.resultMessage = string.Empty;
        this.isTechnicalDetailsVisible = false;
        this.primaryUserSummary = string.Empty;
        this.primaryTitle = string.Empty;
        this.primaryExplanation = string.Empty;
        this.primaryGuidance = string.Empty;
        this.hasGuidance = false;
        this.canRepairPrimaryFinding = false;
        this.isGuidanceVisible = false;
        this.mockRepairStatus = string.Empty;
        this.mockRepairDetail = string.Empty;
        this.isMockRepairRunning = false;
    }

    [ObservableProperty]
    private string appName;

    [ObservableProperty]
    private string appVersion;

    [ObservableProperty]
    private string readyState;

    [ObservableProperty]
    private AppPage currentPage;

    [ObservableProperty]
    private SymptomCategory selectedSymptom;

    [ObservableProperty]
    private string progressText;

    [ObservableProperty]
    private string currentStageText;

    [ObservableProperty]
    private Finding? primaryFinding;

    [ObservableProperty]
    private RepairActionDescriptor? selectedRepairAction;

    [ObservableProperty]
    private string resultMessage;

    [ObservableProperty]
    private bool isTechnicalDetailsVisible;

    /// <summary>普通用户最关注的一句结论（来自首选 Finding 的 UserSummaryKey）。</summary>
    [ObservableProperty]
    private string primaryUserSummary = string.Empty;

    /// <summary>首选 Finding 的标题（技术性标题）。</summary>
    [ObservableProperty]
    private string primaryTitle = string.Empty;

    /// <summary>首选 Finding 的简短说明。</summary>
    [ObservableProperty]
    private string primaryExplanation = string.Empty;

    /// <summary>首选 Finding 的处理方法文案。</summary>
    [ObservableProperty]
    private string primaryGuidance = string.Empty;

    /// <summary>是否拥有可展示的处理方法（GuidanceKey 非空）。</summary>
    [ObservableProperty]
    private bool hasGuidance;

    /// <summary>是否可以对首选 Finding 执行安全修复（动作受支持且非空）。</summary>
    [ObservableProperty]
    private bool canRepairPrimaryFinding;

    /// <summary>处理方法面板是否展开。</summary>
    [ObservableProperty]
    private bool isGuidanceVisible;

    /// <summary>模拟修复状态文案（显示当前事务阶段）。</summary>
    [ObservableProperty]
    private string mockRepairStatus;

    /// <summary>模拟修复详情文案（显示结果或进度说明）。</summary>
    [ObservableProperty]
    private string mockRepairDetail;

    /// <summary>模拟修复是否正在运行（控制按钮可用性和进度条）。</summary>
    [ObservableProperty]
    private bool isMockRepairRunning;

    public ObservableCollection<Finding> allFindings { get; } = [];

    /// <summary>所有症状选项，供 UI 绑定。</summary>
    public static IReadOnlyList<SymptomOption> SymptomOptions { get; } =
    [
        new(SymptomCategory.NothingWorks, Strings.Home_Symptom_NothingWorks),
        new(SymptomCategory.SomeSitesDown, Strings.Home_Symptom_SomeSitesDown),
        new(SymptomCategory.SingleAppDown, Strings.Home_Symptom_SingleAppDown),
        new(SymptomCategory.ProxyVpnOff, Strings.Home_Symptom_ProxyVpnOff),
        new(SymptomCategory.Unsure, Strings.Home_Symptom_Unsure),
    ];

    /// <summary>当前选择的症状选项（用于 UI 单选）。</summary>
    [ObservableProperty]
    private SymptomOption? selectedSymptomOption = SymptomOptions[^1];

    /// <summary>用户输入的目标网站（可选）。对应任务书 §4.2 网址输入框。</summary>
    [ObservableProperty]
    private string targetSiteInput = string.Empty;

    partial void OnSelectedSymptomOptionChanged(SymptomOption? value)
    {
        if (value is not null)
        {
            this.SelectedSymptom = value.Category;
        }
    }

    // --- 命令 ---

    /// <summary>开始体检。切换到 Checking 页，运行真实 Windows 只读探针。</summary>
    [RelayCommand]
    private async Task StartCheckupAsync()
    {
        this.CurrentPage = AppPage.Checking;
        this.ProgressText = string.Format(Strings.Checkup_Progress, 0, 5);
        this.CurrentStageText = Strings.Checkup_Stage1;

        _checkupCts = new CancellationTokenSource();

        try
        {
            // 阶段 2：使用真实 Windows 只读探针
            var env = new WindowsNetworkEnvironment();
            var targetHost = !string.IsNullOrWhiteSpace(this.TargetSiteInput) ? this.TargetSiteInput : null;
            var probes = WindowsProbeSet.BuildQuick(targetHost);
            var orchestrator = new ProbeOrchestrator(probes);

            // 更新进度
            var stageNames = new[]
            {
                Strings.Checkup_Stage1, Strings.Checkup_Stage2, Strings.Checkup_Stage3,
                Strings.Checkup_Stage4, Strings.Checkup_Stage5,
            };
            int stageIndex = 0;
            var progress = new Progress<ProbeProgressEvent>(e =>
            {
                if (e.Stage == ProbeStage.Started && stageIndex < stageNames.Length)
                {
                    this.CurrentStageText = stageNames[stageIndex];
                    this.ProgressText = string.Format(Strings.Checkup_Progress, stageIndex + 1, 5);
                    stageIndex++;
                }
            });

            var result = await orchestrator.ExecuteAsync(
                env,
                this.SelectedSymptom,
                DiagnosticMode.Quick,
                totalBudget: TimeSpan.FromSeconds(30),
                externalCancellationToken: _checkupCts.Token,
                progress: progress);

            // 评估规则
            var snapshot = DiagnosticSnapshot.From(result.Session);
            var registry = BuiltinRuleRegistry.CreateDefault();
            var findings = registry.EvaluateAll(snapshot);

            this.allFindings.Clear();
            foreach (var f in findings)
            {
                this.allFindings.Add(f);
            }

            this.PrimaryFinding = findings.FirstOrDefault();
            this.UpdatePrimaryFindingDisplay();
            this.CurrentPage = AppPage.Result;
        }
        catch (OperationCanceledException)
        {
            this.ResultMessage = Strings.Checkup_Cancelled;
            this.UpdatePrimaryFindingDisplay();
            this.CurrentPage = AppPage.Result;
        }
    }

    /// <summary>
    /// 根据首选 Finding 同步面向用户的展示属性。
    /// 没有结论时构造一个 inconclusive 兜底，保证结果页始终有文案。
    /// </summary>
    private void UpdatePrimaryFindingDisplay()
    {
        var primary = this.PrimaryFinding;
        if (primary is null)
        {
            // 无结论：使用 inconclusive 兜底文案
            this.PrimaryUserSummary = Strings.GetString("finding.inconclusive.summary");
            this.PrimaryTitle = Strings.GetString("finding.inconclusive.title");
            this.PrimaryExplanation = Strings.GetString("finding.inconclusive.explanation");
            this.PrimaryGuidance = Strings.GetString("finding.inconclusive.guidance");
            this.HasGuidance = !string.IsNullOrEmpty(this.PrimaryGuidance);
            this.CanRepairPrimaryFinding = false;
            this.IsGuidanceVisible = false;
            return;
        }

        this.PrimaryUserSummary = Strings.GetString(primary.UserSummaryKey);
        this.PrimaryTitle = Strings.GetString(primary.TitleKey);
        this.PrimaryExplanation = Strings.GetString(primary.ExplanationKey);
        this.PrimaryGuidance = primary.GuidanceKey is null ? string.Empty : Strings.GetString(primary.GuidanceKey);
        this.HasGuidance = !string.IsNullOrEmpty(primary.GuidanceKey);
        // 阶段 3.3：ExecutableRepairActions 为空，首选 Finding 不显示修复按钮。
        this.CanRepairPrimaryFinding = RepairAvailabilityEvaluator.CanExecute(primary, BuiltinRuleRegistry.ExecutableRepairActions);
        this.IsGuidanceVisible = false;
    }

    /// <summary>取消体检。</summary>
    [RelayCommand]
    private void CancelCheckup()
    {
        _checkupCts?.Cancel();
    }

    /// <summary>返回首页。</summary>
    [RelayCommand]
    private void GoHome()
    {
        this.CurrentPage = AppPage.Home;
        this.PrimaryFinding = null;
        this.allFindings.Clear();
        this.SelectedRepairAction = null;
        this.PrimaryUserSummary = string.Empty;
        this.PrimaryTitle = string.Empty;
        this.PrimaryExplanation = string.Empty;
        this.PrimaryGuidance = string.Empty;
        this.HasGuidance = false;
        this.CanRepairPrimaryFinding = false;
        this.IsGuidanceVisible = false;
        this.IsTechnicalDetailsVisible = false;
    }

    /// <summary>查看处理方法：展开引导面板，不返回首页。</summary>
    [RelayCommand]
    private void ViewGuidance()
    {
        this.IsGuidanceVisible = true;
    }

    /// <summary>切换处理方法面板的可见性。</summary>
    [RelayCommand]
    private void ToggleGuidance()
    {
        this.IsGuidanceVisible = !this.IsGuidanceVisible;
    }

    /// <summary>安全修复：进入修复确认页。</summary>
    [RelayCommand]
    private void RequestRepair()
    {
        var actionId = this.PrimaryFinding?.RecommendedActionId;
        if (actionId is null || !BuiltinRuleRegistry.ExecutableRepairActions.Contains(actionId))
        {
            // 阶段 3.3：ExecutableRepairActions 为空，无可执行修复，修复确认页不可达。
            return;
        }

        this.SelectedRepairAction = actionId switch
        {
            "FIX-PRX-01" => RepairActionDescriptor.DisableDeadLocalProxy,
            "FIX-DNS-01" => RepairActionDescriptor.FlushDnsCache,
            _ => null,
        };

        if (this.SelectedRepairAction is not null)
        {
            this.CurrentPage = AppPage.RepairConfirm;
        }
    }

    /// <summary>
    /// 确认执行修复。
    /// 阶段 3.3：无可执行修复动作，修复确认/结果页不可达，此处不做任何操作。
    /// 阶段 4 引入真实 IRepairAction 实现后，再在此处执行修复并跳转 RepairResult。
    /// </summary>
    [RelayCommand]
    private void ConfirmRepair()
    {
        // 阶段 3.3：无可执行修复，不设置假成功文案，不跳转 RepairResult。
        return;
    }

    /// <summary>切换技术详情可见性。</summary>
    [RelayCommand]
    private void ToggleTechnicalDetails()
    {
        this.IsTechnicalDetailsVisible = !this.IsTechnicalDetailsVisible;
    }

    /// <summary>
    /// 导航到模拟修复页面（DryRun 演示，不修改系统）。
    /// 清空上次状态，显示"开始模拟修复"按钮。
    /// </summary>
    [RelayCommand]
    private void NavigateToMockRepair()
    {
        this.MockRepairStatus = Strings.MockRepair_Title;
        this.MockRepairDetail = Strings.MockRepair_NoSystemModification;
        this.IsMockRepairRunning = false;
        this.CurrentPage = AppPage.MockRepair;
    }

    /// <summary>
    /// 执行模拟修复（DryRun 模式）。
    /// 使用 FakeSuccessfulRepairAction 走完整事务流程：
    /// create -> plan -> confirm -> snapshot -> execute -> verify -> 显示结果。
    /// 不修改系统，不调用 Process.Start，不需要提权。
    /// 结果文案明确标注"模拟"和"未对系统进行修改"。
    /// </summary>
    [RelayCommand]
    private async Task MockRepairAsync()
    {
        this.IsMockRepairRunning = true;
        this.MockRepairStatus = Strings.MockRepair_PlanPreview;
        this.MockRepairDetail = string.Empty;

        try
        {
            // 构建测试目录（仅含 Fake 动作，不使用生产目录）
            var catalog = new RepairActionCatalog();
            catalog.Register(new FakeSuccessfulRepairAction());

            var engine = new RepairTransactionEngine(catalog);
            var context = RepairContext.ForDryRun(
                sessionId: "mock-session",
                correlationId: "mock-correlation",
                isElevated: false,
                userConfirmed: false);

            // 1. 创建事务
            engine.CreateTransaction("FAKE-SUCCESS", context);

            // 2. 生成计划
            this.MockRepairStatus = Strings.MockRepair_PlanPreview;
            await engine.PlanAsync(CancellationToken.None);

            // 3. 确认
            engine.Confirm();

            // 4. 捕获快照
            this.MockRepairStatus = Strings.MockRepair_CapturingSnapshot;
            await engine.CaptureSnapshotAsync(CancellationToken.None);

            // 5. 执行
            this.MockRepairStatus = Strings.MockRepair_Executing;
            await engine.ExecuteAsync(CancellationToken.None);

            // 6. 验证
            this.MockRepairStatus = Strings.MockRepair_Verifying;
            var tx = await engine.VerifyAsync(CancellationToken.None);

            // 7. 显示结果
            if (tx.State == RepairTransactionState.Succeeded)
            {
                this.MockRepairStatus = Strings.MockRepair_Success;
                this.MockRepairDetail = Strings.MockRepair_SuccessDetail;
            }
            else if (tx.State == RepairTransactionState.RolledBack)
            {
                this.MockRepairStatus = Strings.MockRepair_RollbackSuccess;
            }
            else if (tx.State == RepairTransactionState.Failed)
            {
                this.MockRepairStatus = Strings.MockRepair_ExecutionFailed;
            }
            else
            {
                this.MockRepairStatus = Strings.MockRepair_ExecutionFailed;
            }
        }
        catch (Exception)
        {
            this.MockRepairStatus = Strings.MockRepair_ExecutionFailed;
            this.MockRepairDetail = Strings.MockRepair_NoSystemModification;
        }
        finally
        {
            this.IsMockRepairRunning = false;
        }
    }

    /// <summary>从模拟修复页返回首页。</summary>
    [RelayCommand]
    private void MockRepairGoHome()
    {
        this.CurrentPage = AppPage.Home;
        this.MockRepairStatus = string.Empty;
        this.MockRepairDetail = string.Empty;
        this.IsMockRepairRunning = false;
    }

    /// <summary>获取 Finding 的标题文案。</summary>
    public string GetFindingTitle(Finding? f) => f is null ? string.Empty : Strings.GetString(f.TitleKey);

    /// <summary>获取 Finding 的说明文案。</summary>
    public string GetFindingExplanation(Finding? f) => f is null ? string.Empty : Strings.GetString(f.ExplanationKey);

    /// <summary>获取可信度文案。</summary>
    public string GetConfidenceText(Confidence c) => c switch
    {
        Confidence.High => Strings.Confidence_High,
        Confidence.Medium => Strings.Confidence_Medium,
        _ => Strings.Confidence_Insufficient,
    };

    /// <summary>获取修复动作标题。</summary>
    public string GetRepairTitle(RepairActionDescriptor? a) => a is null ? string.Empty : Strings.GetString(a.TitleKey);

    /// <summary>获取修复动作描述。</summary>
    public string GetRepairDescription(RepairActionDescriptor? a) => a is null ? string.Empty : Strings.GetString(a.DescriptionKey);

    /// <summary>获取修复确认文案。</summary>
    public string GetRepairConfirmText(RepairActionDescriptor? a) => a is null ? string.Empty : Strings.GetString(a.ConfirmationKey);
}

/// <summary>症状选项。</summary>
public sealed record SymptomOption(SymptomCategory Category, string DisplayText);
