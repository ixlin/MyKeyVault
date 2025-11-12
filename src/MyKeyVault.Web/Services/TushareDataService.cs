using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MyKeyVault.Web.Data;
using MyKeyVault.Web.Models;
using MyKeyVault.Web.Models.Tushare;

namespace MyKeyVault.Web.Services;

/// <summary>
/// Tushare 数据服务（查询、补齐逻辑）
/// </summary>
public class TushareDataService
{
    private readonly ApplicationDbContext _context;
    private readonly TushareApiService _apiService;
    private readonly ILogger<TushareDataService> _logger;

    public TushareDataService(
        ApplicationDbContext context,
        TushareApiService apiService,
        ILogger<TushareDataService> logger)
    {
        _context = context;
        _apiService = apiService;
        _logger = logger;
    }

    /// <summary>
    /// 统一查询接口
    /// </summary>
    public async Task<TushareQueryResponse> QueryAsync(string appId, TushareQueryRequest request)
    {
        var requestId = Guid.NewGuid().ToString("N");
        var startTime = DateTime.UtcNow;

        try
        {
            _logger.LogInformation("开始查询: {ApiName}, AppId: {AppId}, RequestId: {RequestId}", 
                request.ApiName, appId, requestId);

            object? data = request.ApiName.ToLower() switch
            {
                "stock_basic" => await QueryStockBasicAsync(request.Params, request.ForceRefresh),
                "daily" => await QueryStockDailyAsync(request.Params, request.ForceRefresh),
                "income" => await QueryIncomeStatementAsync(request.Params, request.ForceRefresh),
                "balancesheet" => await QueryBalanceSheetAsync(request.Params, request.ForceRefresh),
                "cashflow" => await QueryCashflowStatementAsync(request.Params, request.ForceRefresh),
                _ => throw new NotSupportedException($"不支持的 API: {request.ApiName}")
            };

            await LogCallAsync(appId, request, requestId, startTime, 200, null);

            return new TushareQueryResponse
            {
                Code = 0,
                Message = "success",
                Data = data,
                RequestId = requestId
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "查询失败: {ApiName}, RequestId: {RequestId}", request.ApiName, requestId);

            await LogCallAsync(appId, request, requestId, startTime, 500, ex.Message);

            return new TushareQueryResponse
            {
                Code = 500,
                Message = ex.Message,
                RequestId = requestId
            };
        }
    }

    /// <summary>
    /// 查询股票列表
    /// </summary>
    private async Task<List<StockBasic>> QueryStockBasicAsync(Dictionary<string, object> parameters, bool forceRefresh)
    {
        // 检查是否需要刷新
        if (!forceRefresh)
        {
            var existing = await _context.StockBasics.ToListAsync();
            if (existing.Any())
            {
                _logger.LogInformation("从数据库返回股票列表: {Count} 条", existing.Count);
                return existing;
            }
        }

        // 调用 Tushare API
        _logger.LogInformation("调用 Tushare API 获取股票列表");
        var response = await _apiService.CallApiAsync("stock_basic", parameters);

        if (response.Code != 0 || response.Data == null)
        {
            throw new Exception($"Tushare API 错误: {response.Msg}");
        }

        // 解析并保存
        var stocks = ParseStockBasic(response.Data);
        
        foreach (var stock in stocks)
        {
            var existing = await _context.StockBasics.FindAsync(stock.TsCode);
            if (existing != null)
            {
                // 更新
                existing.Symbol = stock.Symbol;
                existing.Name = stock.Name;
                existing.Area = stock.Area;
                existing.Industry = stock.Industry;
                existing.Fullname = stock.Fullname;
                existing.EnName = stock.EnName;
                existing.Market = stock.Market;
                existing.Exchange = stock.Exchange;
                existing.CurrType = stock.CurrType;
                existing.ListDate = stock.ListDate;
                existing.DelistDate = stock.DelistDate;
                existing.IsHs = stock.IsHs;
                existing.UpdatedAt = DateTime.UtcNow;
            }
            else
            {
                _context.StockBasics.Add(stock);
            }
        }

        await _context.SaveChangesAsync();
        _logger.LogInformation("股票列表已更新: {Count} 条", stocks.Count);

        return stocks;
    }

    /// <summary>
    /// 查询股票日线数据
    /// </summary>
    private async Task<List<StockDaily>> QueryStockDailyAsync(Dictionary<string, object> parameters, bool forceRefresh)
    {
        // 提取参数
        var tsCode = parameters.GetValueOrDefault("ts_code")?.ToString();
        var startDate = parameters.GetValueOrDefault("start_date")?.ToString();
        var endDate = parameters.GetValueOrDefault("end_date")?.ToString();
        var tradeDate = parameters.GetValueOrDefault("trade_date")?.ToString();

        if (string.IsNullOrEmpty(tsCode))
        {
            throw new ArgumentException("参数 ts_code 不能为空");
        }

        // 单日查询
        if (!string.IsNullOrEmpty(tradeDate))
        {
            var date = DateOnly.ParseExact(tradeDate, "yyyyMMdd");
            var existing = await _context.StockDailies
                .Where(d => d.TsCode == tsCode && d.TradeDate == date)
                .FirstOrDefaultAsync();

            if (existing != null && !forceRefresh)
            {
                return new List<StockDaily> { existing };
            }

            // 调用 API
            var response = await _apiService.CallApiAsync("daily", parameters);
            if (response.Code != 0 || response.Data == null)
            {
                throw new Exception($"Tushare API 错误: {response.Msg}");
            }

            var dailies = ParseStockDaily(response.Data);
            await UpsertStockDailiesAsync(dailies);
            return dailies;
        }

        // 范围查询
        if (!string.IsNullOrEmpty(startDate) && !string.IsNullOrEmpty(endDate))
        {
            var start = DateOnly.ParseExact(startDate, "yyyyMMdd");
            var end = DateOnly.ParseExact(endDate, "yyyyMMdd");

            var existing = await _context.StockDailies
                .Where(d => d.TsCode == tsCode && d.TradeDate >= start && d.TradeDate <= end)
                .OrderBy(d => d.TradeDate)
                .ToListAsync();

            // 识别缺失日期（简化版：假设所有交易日都应有数据）
            if (existing.Count == 0 || forceRefresh)
            {
                // 调用 API 获取完整范围
                var response = await _apiService.CallApiAsync("daily", parameters);
                if (response.Code != 0 || response.Data == null)
                {
                    throw new Exception($"Tushare API 错误: {response.Msg}");
                }

                var dailies = ParseStockDaily(response.Data);
                await UpsertStockDailiesAsync(dailies);
                return dailies;
            }

            return existing;
        }

        throw new ArgumentException("参数不完整：需要 trade_date 或 (start_date + end_date)");
    }

    /// <summary>
    /// 查询利润表
    /// </summary>
    private async Task<List<IncomeStatement>> QueryIncomeStatementAsync(Dictionary<string, object> parameters, bool forceRefresh)
    {
        var tsCode = parameters.GetValueOrDefault("ts_code")?.ToString();
        if (string.IsNullOrEmpty(tsCode))
        {
            throw new ArgumentException("参数 ts_code 不能为空");
        }

        // 支持按报告期精确查询
        var period = parameters.GetValueOrDefault("period")?.ToString(); // 如 "20241231"
        var startDate = parameters.GetValueOrDefault("start_date")?.ToString();
        var endDate = parameters.GetValueOrDefault("end_date")?.ToString();

        if (!forceRefresh)
        {
            var query = _context.IncomeStatements.Where(i => i.TsCode == tsCode);

            // 如果指定了 period，只查该报告期
            if (!string.IsNullOrEmpty(period) && DateOnly.TryParseExact(period, "yyyyMMdd", out var periodDate))
            {
                query = query.Where(i => i.EndDate == periodDate);
            }
            // 如果指定了日期范围，查范围内
            else if (!string.IsNullOrEmpty(startDate) && !string.IsNullOrEmpty(endDate))
            {
                if (DateOnly.TryParseExact(startDate, "yyyyMMdd", out var start) &&
                    DateOnly.TryParseExact(endDate, "yyyyMMdd", out var end))
                {
                    query = query.Where(i => i.EndDate >= start && i.EndDate <= end);
                }
            }

            var existing = await query.OrderByDescending(i => i.EndDate).ToListAsync();

            if (existing.Any())
            {
                _logger.LogInformation("从数据库返回利润表: {Count} 条 (ts_code={TsCode}, period={Period})", 
                    existing.Count, tsCode, period ?? "all");
                return existing;
            }
        }

        // 本地没有或强制刷新，调用 API
        var response = await _apiService.CallApiAsync("income", parameters);
        if (response.Code != 0 || response.Data == null)
        {
            throw new Exception($"Tushare API 错误: {response.Msg}");
        }

        var statements = ParseIncomeStatement(response.Data);
        await UpsertIncomeStatementsAsync(statements);
        
        // 返回符合条件的记录（如果有 period 限制，只返回该期间）
        if (!string.IsNullOrEmpty(period) && DateOnly.TryParseExact(period, "yyyyMMdd", out var filterDate))
        {
            return statements.Where(s => s.EndDate == filterDate).ToList();
        }
        return statements;
    }

    /// <summary>
    /// 查询资产负债表
    /// </summary>
    private async Task<List<BalanceSheet>> QueryBalanceSheetAsync(Dictionary<string, object> parameters, bool forceRefresh)
    {
        var tsCode = parameters.GetValueOrDefault("ts_code")?.ToString();
        if (string.IsNullOrEmpty(tsCode))
        {
            throw new ArgumentException("参数 ts_code 不能为空");
        }

        // 支持按报告期精确查询
        var period = parameters.GetValueOrDefault("period")?.ToString();
        var startDate = parameters.GetValueOrDefault("start_date")?.ToString();
        var endDate = parameters.GetValueOrDefault("end_date")?.ToString();

        if (!forceRefresh)
        {
            var query = _context.BalanceSheets.Where(b => b.TsCode == tsCode);

            if (!string.IsNullOrEmpty(period) && DateOnly.TryParseExact(period, "yyyyMMdd", out var periodDate))
            {
                query = query.Where(b => b.EndDate == periodDate);
            }
            else if (!string.IsNullOrEmpty(startDate) && !string.IsNullOrEmpty(endDate))
            {
                if (DateOnly.TryParseExact(startDate, "yyyyMMdd", out var start) &&
                    DateOnly.TryParseExact(endDate, "yyyyMMdd", out var end))
                {
                    query = query.Where(b => b.EndDate >= start && b.EndDate <= end);
                }
            }

            var existing = await query.OrderByDescending(b => b.EndDate).ToListAsync();

            if (existing.Any())
            {
                _logger.LogInformation("从数据库返回资产负债表: {Count} 条 (ts_code={TsCode}, period={Period})", 
                    existing.Count, tsCode, period ?? "all");
                return existing;
            }
        }

        var response = await _apiService.CallApiAsync("balancesheet", parameters);
        if (response.Code != 0 || response.Data == null)
        {
            throw new Exception($"Tushare API 错误: {response.Msg}");
        }

        var statements = ParseBalanceSheet(response.Data);
        await UpsertBalanceSheetsAsync(statements);
        return statements;
    }

    /// <summary>
    /// 查询现金流量表
    /// </summary>
    private async Task<List<CashflowStatement>> QueryCashflowStatementAsync(Dictionary<string, object> parameters, bool forceRefresh)
    {
        var tsCode = parameters.GetValueOrDefault("ts_code")?.ToString();
        if (string.IsNullOrEmpty(tsCode))
        {
            throw new ArgumentException("参数 ts_code 不能为空");
        }

        // 支持按报告期精确查询
        var period = parameters.GetValueOrDefault("period")?.ToString();
        var startDate = parameters.GetValueOrDefault("start_date")?.ToString();
        var endDate = parameters.GetValueOrDefault("end_date")?.ToString();

        if (!forceRefresh)
        {
            var query = _context.CashflowStatements.Where(c => c.TsCode == tsCode);

            if (!string.IsNullOrEmpty(period) && DateOnly.TryParseExact(period, "yyyyMMdd", out var periodDate))
            {
                query = query.Where(c => c.EndDate == periodDate);
            }
            else if (!string.IsNullOrEmpty(startDate) && !string.IsNullOrEmpty(endDate))
            {
                if (DateOnly.TryParseExact(startDate, "yyyyMMdd", out var start) &&
                    DateOnly.TryParseExact(endDate, "yyyyMMdd", out var end))
                {
                    query = query.Where(c => c.EndDate >= start && c.EndDate <= end);
                }
            }

            var existing = await query.OrderByDescending(c => c.EndDate).ToListAsync();

            if (existing.Any())
            {
                _logger.LogInformation("从数据库返回现金流量表: {Count} 条 (ts_code={TsCode}, period={Period})", 
                    existing.Count, tsCode, period ?? "all");
                return existing;
            }
        }

        var response = await _apiService.CallApiAsync("cashflow", parameters);
        if (response.Code != 0 || response.Data == null)
        {
            throw new Exception($"Tushare API 错误: {response.Msg}");
        }

        var statements = ParseCashflowStatement(response.Data);
        await UpsertCashflowStatementsAsync(statements);
        
        // 返回符合条件的记录
        if (!string.IsNullOrEmpty(period) && DateOnly.TryParseExact(period, "yyyyMMdd", out var filterDate))
        {
            return statements.Where(s => s.EndDate == filterDate).ToList();
        }
        return statements;
    }

    // ==================== 数据解析方法 ====================

    private List<StockBasic> ParseStockBasic(TushareApiData data)
    {
        var result = new List<StockBasic>();
        var fieldMap = MapFields(data.Fields);

        foreach (var item in data.Items)
        {
            var stock = new StockBasic
            {
                TsCode = GetString(item, fieldMap, "ts_code") ?? "",
                Symbol = GetString(item, fieldMap, "symbol"),
                Name = GetString(item, fieldMap, "name"),
                Area = GetString(item, fieldMap, "area"),
                Industry = GetString(item, fieldMap, "industry"),
                Fullname = GetString(item, fieldMap, "fullname"),
                EnName = GetString(item, fieldMap, "enname"),
                Market = GetString(item, fieldMap, "market"),
                Exchange = GetString(item, fieldMap, "exchange"),
                CurrType = GetString(item, fieldMap, "curr_type"),
                ListDate = GetDate(item, fieldMap, "list_date"),
                DelistDate = GetDate(item, fieldMap, "delist_date"),
                IsHs = GetString(item, fieldMap, "is_hs"),
                UpdatedAt = DateTime.UtcNow
            };
            result.Add(stock);
        }

        return result;
    }

    private List<StockDaily> ParseStockDaily(TushareApiData data)
    {
        var result = new List<StockDaily>();
        var fieldMap = MapFields(data.Fields);

        foreach (var item in data.Items)
        {
            var daily = new StockDaily
            {
                TsCode = GetString(item, fieldMap, "ts_code") ?? "",
                TradeDate = GetDate(item, fieldMap, "trade_date") ?? DateOnly.MinValue,
                Open = GetDecimal(item, fieldMap, "open"),
                High = GetDecimal(item, fieldMap, "high"),
                Low = GetDecimal(item, fieldMap, "low"),
                Close = GetDecimal(item, fieldMap, "close"),
                PreClose = GetDecimal(item, fieldMap, "pre_close"),
                Change = GetDecimal(item, fieldMap, "change"),
                PctChg = GetDecimal(item, fieldMap, "pct_chg"),
                Vol = GetDecimal(item, fieldMap, "vol"),
                Amount = GetDecimal(item, fieldMap, "amount"),
                SourceUpdatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            result.Add(daily);
        }

        return result;
    }

    private List<IncomeStatement> ParseIncomeStatement(TushareApiData data)
    {
        var result = new List<IncomeStatement>();
        var fieldMap = MapFields(data.Fields);

        foreach (var item in data.Items)
        {
            var statement = new IncomeStatement
            {
                // 基础字段
                TsCode = GetString(item, fieldMap, "ts_code") ?? "",
                AnnDate = GetString(item, fieldMap, "ann_date"),
                FAnnDate = GetString(item, fieldMap, "f_ann_date"),
                EndDate = GetDate(item, fieldMap, "end_date") ?? DateOnly.MinValue,
                ReportType = GetString(item, fieldMap, "report_type"),
                CompType = GetString(item, fieldMap, "comp_type"),
                EndType = GetString(item, fieldMap, "end_type"),
                
                // 每股指标
                BasicEps = GetDecimal(item, fieldMap, "basic_eps"),
                DilutedEps = GetDecimal(item, fieldMap, "diluted_eps"),
                
                // 收入项目
                TotalRevenue = GetDecimal(item, fieldMap, "total_revenue"),
                Revenue = GetDecimal(item, fieldMap, "revenue"),
                IntIncome = GetDecimal(item, fieldMap, "int_income"),
                PremEarned = GetDecimal(item, fieldMap, "prem_earned"),
                CommIncome = GetDecimal(item, fieldMap, "comm_income"),
                NCommisIncome = GetDecimal(item, fieldMap, "n_commis_income"),
                NOthIncome = GetDecimal(item, fieldMap, "n_oth_income"),
                NOthBIncome = GetDecimal(item, fieldMap, "n_oth_b_income"),
                PremIncome = GetDecimal(item, fieldMap, "prem_income"),
                OutPrem = GetDecimal(item, fieldMap, "out_prem"),
                UnePremReser = GetDecimal(item, fieldMap, "une_prem_reser"),
                ReinsIncome = GetDecimal(item, fieldMap, "reins_income"),
                NSecTbIncome = GetDecimal(item, fieldMap, "n_sec_tb_income"),
                NSecUwIncome = GetDecimal(item, fieldMap, "n_sec_uw_income"),
                NAssetMgIncome = GetDecimal(item, fieldMap, "n_asset_mg_income"),
                OthBIncome = GetDecimal(item, fieldMap, "oth_b_income"),
                FvValueChgGain = GetDecimal(item, fieldMap, "fv_value_chg_gain"),
                InvestIncome = GetDecimal(item, fieldMap, "invest_income"),
                AssInvestIncome = GetDecimal(item, fieldMap, "ass_invest_income"),
                ForexGain = GetDecimal(item, fieldMap, "forex_gain"),
                
                // 成本费用
                TotalCogs = GetDecimal(item, fieldMap, "total_cogs"),
                OperCost = GetDecimal(item, fieldMap, "oper_cost"),
                IntExp = GetDecimal(item, fieldMap, "int_exp"),
                CommExp = GetDecimal(item, fieldMap, "comm_exp"),
                BizTaxSurchg = GetDecimal(item, fieldMap, "biz_tax_surchg"),
                SellExp = GetDecimal(item, fieldMap, "sell_exp"),
                AdminExp = GetDecimal(item, fieldMap, "admin_exp"),
                FinExp = GetDecimal(item, fieldMap, "fin_exp"),
                AssetsImpairLoss = GetDecimal(item, fieldMap, "assets_impair_loss"),
                PremRefund = GetDecimal(item, fieldMap, "prem_refund"),
                CompensPayout = GetDecimal(item, fieldMap, "compens_payout"),
                ReserInsurLiab = GetDecimal(item, fieldMap, "reser_insur_liab"),
                DivPayt = GetDecimal(item, fieldMap, "div_payt"),
                ReinsExp = GetDecimal(item, fieldMap, "reins_exp"),
                OperExp = GetDecimal(item, fieldMap, "oper_exp"),
                CompensPayoutRefu = GetDecimal(item, fieldMap, "compens_payout_refu"),
                InsurReserRefu = GetDecimal(item, fieldMap, "insur_reser_refu"),
                ReinsCostRefund = GetDecimal(item, fieldMap, "reins_cost_refund"),
                OtherBusCost = GetDecimal(item, fieldMap, "other_bus_cost"),
                
                // 利润项目
                OperateProfit = GetDecimal(item, fieldMap, "operate_profit"),
                NonOperIncome = GetDecimal(item, fieldMap, "non_oper_income"),
                NonOperExp = GetDecimal(item, fieldMap, "non_oper_exp"),
                NcaDisploss = GetDecimal(item, fieldMap, "nca_disploss"),
                TotalProfit = GetDecimal(item, fieldMap, "total_profit"),
                IncomeTax = GetDecimal(item, fieldMap, "income_tax"),
                NIncome = GetDecimal(item, fieldMap, "n_income"),
                NIncomeAttrP = GetDecimal(item, fieldMap, "n_income_attr_p"),
                MinorityGain = GetDecimal(item, fieldMap, "minority_gain"),
                OthComprIncome = GetDecimal(item, fieldMap, "oth_compr_income"),
                TComprIncome = GetDecimal(item, fieldMap, "t_compr_income"),
                ComprIncAttrP = GetDecimal(item, fieldMap, "compr_inc_attr_p"),
                ComprIncAttrMS = GetDecimal(item, fieldMap, "compr_inc_attr_m_s"),
                
                // 其他指标
                Ebit = GetDecimal(item, fieldMap, "ebit"),
                Ebitda = GetDecimal(item, fieldMap, "ebitda"),
                InsuranceExp = GetDecimal(item, fieldMap, "insurance_exp"),
                UndistProfit = GetDecimal(item, fieldMap, "undist_profit"),
                DistableProfit = GetDecimal(item, fieldMap, "distable_profit"),
                RdExp = GetDecimal(item, fieldMap, "rd_exp"),
                FinExpIntExp = GetDecimal(item, fieldMap, "fin_exp_int_exp"),
                FinExpIntInc = GetDecimal(item, fieldMap, "fin_exp_int_inc"),
                
                // 利润分配
                TransferSurplusRese = GetDecimal(item, fieldMap, "transfer_surplus_rese"),
                TransferHousingImprest = GetDecimal(item, fieldMap, "transfer_housing_imprest"),
                TransferOth = GetDecimal(item, fieldMap, "transfer_oth"),
                AdjLossgain = GetDecimal(item, fieldMap, "adj_lossgain"),
                WithdraLegalSurplus = GetDecimal(item, fieldMap, "withdra_legal_surplus"),
                WithdraLegalPubfund = GetDecimal(item, fieldMap, "withdra_legal_pubfund"),
                WithdraBizDevfund = GetDecimal(item, fieldMap, "withdra_biz_devfund"),
                WithdraReseFund = GetDecimal(item, fieldMap, "withdra_rese_fund"),
                WithdraOthErsu = GetDecimal(item, fieldMap, "withdra_oth_ersu"),
                WorkersWelfare = GetDecimal(item, fieldMap, "workers_welfare"),
                DistrProfitShrhder = GetDecimal(item, fieldMap, "distr_profit_shrhder"),
                PrfsharePayableDvd = GetDecimal(item, fieldMap, "prfshare_payable_dvd"),
                ComsharePayableDvd = GetDecimal(item, fieldMap, "comshare_payable_dvd"),
                CapitComstockDiv = GetDecimal(item, fieldMap, "capit_comstock_div"),
                
                // 扩展字段
                NetAfterNrLpCorrect = GetDecimal(item, fieldMap, "net_after_nr_lp_correct"),
                CreditImpaLoss = GetDecimal(item, fieldMap, "credit_impa_loss"),
                NetExpoHedgingBenefits = GetDecimal(item, fieldMap, "net_expo_hedging_benefits"),
                OthImpairLossAssets = GetDecimal(item, fieldMap, "oth_impair_loss_assets"),
                TotalOpcost = GetDecimal(item, fieldMap, "total_opcost"),
                AmodcostFinAssets = GetDecimal(item, fieldMap, "amodcost_fin_assets"),
                OthIncome = GetDecimal(item, fieldMap, "oth_income"),
                AssetDispIncome = GetDecimal(item, fieldMap, "asset_disp_income"),
                ContinuedNetProfit = GetDecimal(item, fieldMap, "continued_net_profit"),
                EndNetProfit = GetDecimal(item, fieldMap, "end_net_profit"),
                UpdateFlag = GetString(item, fieldMap, "update_flag"),
                
                UpdatedAt = DateTime.UtcNow
            };
            result.Add(statement);
        }

        return result;
    }

    private List<BalanceSheet> ParseBalanceSheet(TushareApiData data)
    {
        var result = new List<BalanceSheet>();
        var fieldMap = MapFields(data.Fields);

        foreach (var item in data.Items)
        {
            var sheet = new BalanceSheet
            {
                // 基础字段
                TsCode = GetString(item, fieldMap, "ts_code") ?? "",
                AnnDate = GetString(item, fieldMap, "ann_date"),
                FAnnDate = GetString(item, fieldMap, "f_ann_date"),
                EndDate = GetDate(item, fieldMap, "end_date") ?? DateOnly.MinValue,
                ReportType = GetString(item, fieldMap, "report_type"),
                CompType = GetString(item, fieldMap, "comp_type"),
                EndType = GetString(item, fieldMap, "end_type"),
                
                // 股本及储备
                TotalShare = GetDecimal(item, fieldMap, "total_share"),
                CapRese = GetDecimal(item, fieldMap, "cap_rese"),
                UndistrPorfit = GetDecimal(item, fieldMap, "undistr_porfit"),
                SurplusRese = GetDecimal(item, fieldMap, "surplus_rese"),
                SpecialRese = GetDecimal(item, fieldMap, "special_rese"),
                
                // 流动资产
                MoneyCap = GetDecimal(item, fieldMap, "money_cap"),
                TradAsset = GetDecimal(item, fieldMap, "trad_asset"),
                NotesReceiv = GetDecimal(item, fieldMap, "notes_receiv"),
                AccountsReceiv = GetDecimal(item, fieldMap, "accounts_receiv"),
                OthReceiv = GetDecimal(item, fieldMap, "oth_receiv"),
                Prepayment = GetDecimal(item, fieldMap, "prepayment"),
                DivReceiv = GetDecimal(item, fieldMap, "div_receiv"),
                IntReceiv = GetDecimal(item, fieldMap, "int_receiv"),
                Inventories = GetDecimal(item, fieldMap, "inventories"),
                AmorExp = GetDecimal(item, fieldMap, "amor_exp"),
                NcaWithin1y = GetDecimal(item, fieldMap, "nca_within_1y"),
                SettRsrv = GetDecimal(item, fieldMap, "sett_rsrv"),
                LoantoOthBankFi = GetDecimal(item, fieldMap, "loanto_oth_bank_fi"),
                PremiumReceiv = GetDecimal(item, fieldMap, "premium_receiv"),
                ReinsurReceiv = GetDecimal(item, fieldMap, "reinsur_receiv"),
                ReinsurResReceiv = GetDecimal(item, fieldMap, "reinsur_res_receiv"),
                PurResaleFa = GetDecimal(item, fieldMap, "pur_resale_fa"),
                OthCurAssets = GetDecimal(item, fieldMap, "oth_cur_assets"),
                TotalCurAssets = GetDecimal(item, fieldMap, "total_cur_assets"),
                
                // 非流动资产
                FaAvailForSale = GetDecimal(item, fieldMap, "fa_avail_for_sale"),
                HtmInvest = GetDecimal(item, fieldMap, "htm_invest"),
                LtEqtInvest = GetDecimal(item, fieldMap, "lt_eqt_invest"),
                InvestRealEstate = GetDecimal(item, fieldMap, "invest_real_estate"),
                TimeDeposits = GetDecimal(item, fieldMap, "time_deposits"),
                OthAssets = GetDecimal(item, fieldMap, "oth_assets"),
                LtRec = GetDecimal(item, fieldMap, "lt_rec"),
                FixAssets = GetDecimal(item, fieldMap, "fix_assets"),
                Cip = GetDecimal(item, fieldMap, "cip"),
                ConstMaterials = GetDecimal(item, fieldMap, "const_materials"),
                FixedAssetsDisp = GetDecimal(item, fieldMap, "fixed_assets_disp"),
                ProducBioAssets = GetDecimal(item, fieldMap, "produc_bio_assets"),
                OilAndGasAssets = GetDecimal(item, fieldMap, "oil_and_gas_assets"),
                IntanAssets = GetDecimal(item, fieldMap, "intan_assets"),
                RAndD = GetDecimal(item, fieldMap, "r_and_d"),
                Goodwill = GetDecimal(item, fieldMap, "goodwill"),
                LtAmorExp = GetDecimal(item, fieldMap, "lt_amor_exp"),
                DeferTaxAssets = GetDecimal(item, fieldMap, "defer_tax_assets"),
                DecrInDisbur = GetDecimal(item, fieldMap, "decr_in_disbur"),
                OthNca = GetDecimal(item, fieldMap, "oth_nca"),
                TotalNca = GetDecimal(item, fieldMap, "total_nca"),
                
                // 银行/金融/保险特有资产
                CashReserCb = GetDecimal(item, fieldMap, "cash_reser_cb"),
                DeposInOthBfi = GetDecimal(item, fieldMap, "depos_in_oth_bfi"),
                PrecMetals = GetDecimal(item, fieldMap, "prec_metals"),
                DerivAssets = GetDecimal(item, fieldMap, "deriv_assets"),
                RrReinsUnePrem = GetDecimal(item, fieldMap, "rr_reins_une_prem"),
                RrReinsOutstdCla = GetDecimal(item, fieldMap, "rr_reins_outstd_cla"),
                RrReinsLinsLiab = GetDecimal(item, fieldMap, "rr_reins_lins_liab"),
                RrReinsLthinsLiab = GetDecimal(item, fieldMap, "rr_reins_lthins_liab"),
                RefundDepos = GetDecimal(item, fieldMap, "refund_depos"),
                PhPledgeLoans = GetDecimal(item, fieldMap, "ph_pledge_loans"),
                RefundCapDepos = GetDecimal(item, fieldMap, "refund_cap_depos"),
                IndepAcctAssets = GetDecimal(item, fieldMap, "indep_acct_assets"),
                ClientDepos = GetDecimal(item, fieldMap, "client_depos"),
                ClientProv = GetDecimal(item, fieldMap, "client_prov"),
                TransacSeatFee = GetDecimal(item, fieldMap, "transac_seat_fee"),
                InvestAsReceiv = GetDecimal(item, fieldMap, "invest_as_receiv"),
                
                // 资产总计
                TotalAssets = GetDecimal(item, fieldMap, "total_assets"),
                
                // 流动负债
                LtBorr = GetDecimal(item, fieldMap, "lt_borr"),
                StBorr = GetDecimal(item, fieldMap, "st_borr"),
                CbBorr = GetDecimal(item, fieldMap, "cb_borr"),
                DeposIbDeposits = GetDecimal(item, fieldMap, "depos_ib_deposits"),
                LoanOthBank = GetDecimal(item, fieldMap, "loan_oth_bank"),
                TradingFl = GetDecimal(item, fieldMap, "trading_fl"),
                NotesPayable = GetDecimal(item, fieldMap, "notes_payable"),
                AcctPayable = GetDecimal(item, fieldMap, "acct_payable"),
                AdvReceipts = GetDecimal(item, fieldMap, "adv_receipts"),
                SoldForRepurFa = GetDecimal(item, fieldMap, "sold_for_repur_fa"),
                CommPayable = GetDecimal(item, fieldMap, "comm_payable"),
                PayrollPayable = GetDecimal(item, fieldMap, "payroll_payable"),
                TaxesPayable = GetDecimal(item, fieldMap, "taxes_payable"),
                IntPayable = GetDecimal(item, fieldMap, "int_payable"),
                DivPayable = GetDecimal(item, fieldMap, "div_payable"),
                OthPayable = GetDecimal(item, fieldMap, "oth_payable"),
                AccExp = GetDecimal(item, fieldMap, "acc_exp"),
                DeferredInc = GetDecimal(item, fieldMap, "deferred_inc"),
                StBondsPayable = GetDecimal(item, fieldMap, "st_bonds_payable"),
                PayableToReinsurer = GetDecimal(item, fieldMap, "payable_to_reinsurer"),
                RsrvInsurCont = GetDecimal(item, fieldMap, "rsrv_insur_cont"),
                ActingTradingSec = GetDecimal(item, fieldMap, "acting_trading_sec"),
                ActingUwSec = GetDecimal(item, fieldMap, "acting_uw_sec"),
                NonCurLiabDue1y = GetDecimal(item, fieldMap, "non_cur_liab_due_1y"),
                OthCurLiab = GetDecimal(item, fieldMap, "oth_cur_liab"),
                TotalCurLiab = GetDecimal(item, fieldMap, "total_cur_liab"),
                
                // 非流动负债
                BondPayable = GetDecimal(item, fieldMap, "bond_payable"),
                LtPayable = GetDecimal(item, fieldMap, "lt_payable"),
                SpecificPayables = GetDecimal(item, fieldMap, "specific_payables"),
                EstimatedLiab = GetDecimal(item, fieldMap, "estimated_liab"),
                DeferTaxLiab = GetDecimal(item, fieldMap, "defer_tax_liab"),
                DeferIncNonCurLiab = GetDecimal(item, fieldMap, "defer_inc_non_cur_liab"),
                OthNcl = GetDecimal(item, fieldMap, "oth_ncl"),
                TotalNcl = GetDecimal(item, fieldMap, "total_ncl"),
                
                // 银行/金融/保险特有负债
                DeposOthBfi = GetDecimal(item, fieldMap, "depos_oth_bfi"),
                DerivLiab = GetDecimal(item, fieldMap, "deriv_liab"),
                Depos = GetDecimal(item, fieldMap, "depos"),
                AgencyBusLiab = GetDecimal(item, fieldMap, "agency_bus_liab"),
                OthLiab = GetDecimal(item, fieldMap, "oth_liab"),
                PremReceivAdva = GetDecimal(item, fieldMap, "prem_receiv_adva"),
                DeposReceived = GetDecimal(item, fieldMap, "depos_received"),
                PhInvest = GetDecimal(item, fieldMap, "ph_invest"),
                ReserUnePrem = GetDecimal(item, fieldMap, "reser_une_prem"),
                ReserOutstdClaims = GetDecimal(item, fieldMap, "reser_outstd_claims"),
                ReserLinsLiab = GetDecimal(item, fieldMap, "reser_lins_liab"),
                ReserLthinsLiab = GetDecimal(item, fieldMap, "reser_lthins_liab"),
                IndeptAccLiab = GetDecimal(item, fieldMap, "indept_acc_liab"),
                PledgeBorr = GetDecimal(item, fieldMap, "pledge_borr"),
                IndemPayable = GetDecimal(item, fieldMap, "indem_payable"),
                PolicyDivPayable = GetDecimal(item, fieldMap, "policy_div_payable"),
                
                // 负债合计
                TotalLiab = GetDecimal(item, fieldMap, "total_liab"),
                
                // 所有者权益
                TreasuryShare = GetDecimal(item, fieldMap, "treasury_share"),
                OrdinRiskReser = GetDecimal(item, fieldMap, "ordin_risk_reser"),
                ForexDiffer = GetDecimal(item, fieldMap, "forex_differ"),
                InvestLossUnconf = GetDecimal(item, fieldMap, "invest_loss_unconf"),
                MinorityInt = GetDecimal(item, fieldMap, "minority_int"),
                TotalHldrEqyExcMinInt = GetDecimal(item, fieldMap, "total_hldr_eqy_exc_min_int"),
                TotalHldrEqyIncMinInt = GetDecimal(item, fieldMap, "total_hldr_eqy_inc_min_int"),
                
                // 负债及股东权益总计
                TotalLiabHldrEqy = GetDecimal(item, fieldMap, "total_liab_hldr_eqy"),
                
                // 补充字段
                LtPayrollPayable = GetDecimal(item, fieldMap, "lt_payroll_payable"),
                OthCompIncome = GetDecimal(item, fieldMap, "oth_comp_income"),
                OthEqtTools = GetDecimal(item, fieldMap, "oth_eqt_tools"),
                OthEqtToolsPShr = GetDecimal(item, fieldMap, "oth_eqt_tools_p_shr"),
                LendingFunds = GetDecimal(item, fieldMap, "lending_funds"),
                AccReceivable = GetDecimal(item, fieldMap, "acc_receivable"),
                StFinPayable = GetDecimal(item, fieldMap, "st_fin_payable"),
                Payables = GetDecimal(item, fieldMap, "payables"),
                HfsAssets = GetDecimal(item, fieldMap, "hfs_assets"),
                HfsSales = GetDecimal(item, fieldMap, "hfs_sales"),
                CostFinAssets = GetDecimal(item, fieldMap, "cost_fin_assets"),
                FairValueFinAssets = GetDecimal(item, fieldMap, "fair_value_fin_assets"),
                CipTotal = GetDecimal(item, fieldMap, "cip_total"),
                OthPayTotal = GetDecimal(item, fieldMap, "oth_pay_total"),
                LongPayTotal = GetDecimal(item, fieldMap, "long_pay_total"),
                DebtInvest = GetDecimal(item, fieldMap, "debt_invest"),
                OthDebtInvest = GetDecimal(item, fieldMap, "oth_debt_invest"),
                OthEqInvest = GetDecimal(item, fieldMap, "oth_eq_invest"),
                OthIlliqFinAssets = GetDecimal(item, fieldMap, "oth_illiq_fin_assets"),
                OthEqPpbond = GetDecimal(item, fieldMap, "oth_eq_ppbond"),
                ReceivFinancing = GetDecimal(item, fieldMap, "receiv_financing"),
                UseRightAssets = GetDecimal(item, fieldMap, "use_right_assets"),
                LeaseLiab = GetDecimal(item, fieldMap, "lease_liab"),
                ContractAssets = GetDecimal(item, fieldMap, "contract_assets"),
                ContractLiab = GetDecimal(item, fieldMap, "contract_liab"),
                AccountsReceivBill = GetDecimal(item, fieldMap, "accounts_receiv_bill"),
                AccountsPay = GetDecimal(item, fieldMap, "accounts_pay"),
                OthRcvTotal = GetDecimal(item, fieldMap, "oth_rcv_total"),
                FixAssetsTotal = GetDecimal(item, fieldMap, "fix_assets_total"),
                UpdateFlag = GetString(item, fieldMap, "update_flag"),
                
                UpdatedAt = DateTime.UtcNow
            };
            result.Add(sheet);
        }

        return result;
    }

    private List<CashflowStatement> ParseCashflowStatement(TushareApiData data)
    {
        var result = new List<CashflowStatement>();
        var fieldMap = MapFields(data.Fields);

        foreach (var item in data.Items)
        {
            var statement = new CashflowStatement
            {
                // 基础字段
                TsCode = GetString(item, fieldMap, "ts_code") ?? "",
                AnnDate = GetString(item, fieldMap, "ann_date"),
                FAnnDate = GetString(item, fieldMap, "f_ann_date"),
                EndDate = GetDate(item, fieldMap, "end_date") ?? DateOnly.MinValue,
                CompType = GetString(item, fieldMap, "comp_type"),
                ReportType = GetString(item, fieldMap, "report_type"),
                EndType = GetString(item, fieldMap, "end_type"),
                
                // 间接法补充项
                NetProfit = GetDecimal(item, fieldMap, "net_profit"),
                FinanExp = GetDecimal(item, fieldMap, "finan_exp"),
                
                // 经营活动现金流
                CFrSaleSg = GetDecimal(item, fieldMap, "c_fr_sale_sg"),
                RecpTaxRends = GetDecimal(item, fieldMap, "recp_tax_rends"),
                NDeposIncrFi = GetDecimal(item, fieldMap, "n_depos_incr_fi"),
                NIncrLoansCb = GetDecimal(item, fieldMap, "n_incr_loans_cb"),
                NIncBorrOthFi = GetDecimal(item, fieldMap, "n_inc_borr_oth_fi"),
                PremFrOrigContr = GetDecimal(item, fieldMap, "prem_fr_orig_contr"),
                NIncrInsuredDep = GetDecimal(item, fieldMap, "n_incr_insured_dep"),
                NReinsurPrem = GetDecimal(item, fieldMap, "n_reinsur_prem"),
                NIncrDispTfa = GetDecimal(item, fieldMap, "n_incr_disp_tfa"),
                IfcCashIncr = GetDecimal(item, fieldMap, "ifc_cash_incr"),
                NIncrDispFaas = GetDecimal(item, fieldMap, "n_incr_disp_faas"),
                NIncrLoansOthBank = GetDecimal(item, fieldMap, "n_incr_loans_oth_bank"),
                NCapIncrRepur = GetDecimal(item, fieldMap, "n_cap_incr_repur"),
                CFrOthOperateA = GetDecimal(item, fieldMap, "c_fr_oth_operate_a"),
                CInfFrOperateA = GetDecimal(item, fieldMap, "c_inf_fr_operate_a"),
                CPaidGoodsS = GetDecimal(item, fieldMap, "c_paid_goods_s"),
                CPaidToForEmpl = GetDecimal(item, fieldMap, "c_paid_to_for_empl"),
                CPaidForTaxes = GetDecimal(item, fieldMap, "c_paid_for_taxes"),
                NIncrCltLoanAdv = GetDecimal(item, fieldMap, "n_incr_clt_loan_adv"),
                NIncrDepCbob = GetDecimal(item, fieldMap, "n_incr_dep_cbob"),
                CPayClaimsOrigInco = GetDecimal(item, fieldMap, "c_pay_claims_orig_inco"),
                PayHandlingChrg = GetDecimal(item, fieldMap, "pay_handling_chrg"),
                PayCommInsurPlcy = GetDecimal(item, fieldMap, "pay_comm_insur_plcy"),
                OthCashPayOperAct = GetDecimal(item, fieldMap, "oth_cash_pay_oper_act"),
                StCashOutAct = GetDecimal(item, fieldMap, "st_cash_out_act"),
                NCashflowAct = GetDecimal(item, fieldMap, "n_cashflow_act"),
                
                // 投资活动现金流
                OthRecpRalInvAct = GetDecimal(item, fieldMap, "oth_recp_ral_inv_act"),
                CDispWithdrwlInvest = GetDecimal(item, fieldMap, "c_disp_withdrwl_invest"),
                CRecpReturnInvest = GetDecimal(item, fieldMap, "c_recp_return_invest"),
                NRecpDispFilta = GetDecimal(item, fieldMap, "n_recp_disp_fiolta"),
                NRecpDispSobu = GetDecimal(item, fieldMap, "n_recp_disp_sobu"),
                StotInflowsInvAct = GetDecimal(item, fieldMap, "stot_inflows_inv_act"),
                CPayAcqConstFilta = GetDecimal(item, fieldMap, "c_pay_acq_const_fiolta"),
                CPaidInvest = GetDecimal(item, fieldMap, "c_paid_invest"),
                NDispSubsOthBiz = GetDecimal(item, fieldMap, "n_disp_subs_oth_biz"),
                OthPayRalInvAct = GetDecimal(item, fieldMap, "oth_pay_ral_inv_act"),
                NIncrPledgeLoan = GetDecimal(item, fieldMap, "n_incr_pledge_loan"),
                StotOutInvAct = GetDecimal(item, fieldMap, "stot_out_inv_act"),
                NCashflowInvAct = GetDecimal(item, fieldMap, "n_cashflow_inv_act"),
                
                // 筹资活动现金流
                CRecpBorrow = GetDecimal(item, fieldMap, "c_recp_borrow"),
                ProcIssueBonds = GetDecimal(item, fieldMap, "proc_issue_bonds"),
                OthCashRecpRalFncAct = GetDecimal(item, fieldMap, "oth_cash_recp_ral_fnc_act"),
                StotCashInFncAct = GetDecimal(item, fieldMap, "stot_cash_in_fnc_act"),
                FreeCashflow = GetDecimal(item, fieldMap, "free_cashflow"),
                CPrepayAmtBorr = GetDecimal(item, fieldMap, "c_prepay_amt_borr"),
                CPayDistDpcpIntExp = GetDecimal(item, fieldMap, "c_pay_dist_dpcp_int_exp"),
                InclDvdProfitPaidScMs = GetDecimal(item, fieldMap, "incl_dvd_profit_paid_sc_ms"),
                OthCashpayRalFncAct = GetDecimal(item, fieldMap, "oth_cashpay_ral_fnc_act"),
                StotCashoutFncAct = GetDecimal(item, fieldMap, "stot_cashout_fnc_act"),
                NCashFlowsFncAct = GetDecimal(item, fieldMap, "n_cash_flows_fnc_act"),
                
                // 汇率及现金净增加
                EffFxFluCash = GetDecimal(item, fieldMap, "eff_fx_flu_cash"),
                NIncrCashCashEqu = GetDecimal(item, fieldMap, "n_incr_cash_cash_equ"),
                CCashEquBegPeriod = GetDecimal(item, fieldMap, "c_cash_equ_beg_period"),
                CCashEquEndPeriod = GetDecimal(item, fieldMap, "c_cash_equ_end_period"),
                
                // 补充资料
                CRecpCapContrib = GetDecimal(item, fieldMap, "c_recp_cap_contrib"),
                InclCashRecSaims = GetDecimal(item, fieldMap, "incl_cash_rec_saims"),
                UnconInvestLoss = GetDecimal(item, fieldMap, "uncon_invest_loss"),
                ProvDeprAssets = GetDecimal(item, fieldMap, "prov_depr_assets"),
                DeprFaCogaDpba = GetDecimal(item, fieldMap, "depr_fa_coga_dpba"),
                AmortIntangAssets = GetDecimal(item, fieldMap, "amort_intang_assets"),
                LtAmortDeferredExp = GetDecimal(item, fieldMap, "lt_amort_deferred_exp"),
                DecrDeferredExp = GetDecimal(item, fieldMap, "decr_deferred_exp"),
                IncrAccExp = GetDecimal(item, fieldMap, "incr_acc_exp"),
                LossDispFilta = GetDecimal(item, fieldMap, "loss_disp_fiolta"),
                LossScrFa = GetDecimal(item, fieldMap, "loss_scr_fa"),
                LossFvChg = GetDecimal(item, fieldMap, "loss_fv_chg"),
                InvestLoss = GetDecimal(item, fieldMap, "invest_loss"),
                DecrDefIncTaxAssets = GetDecimal(item, fieldMap, "decr_def_inc_tax_assets"),
                IncrDefIncTaxLiab = GetDecimal(item, fieldMap, "incr_def_inc_tax_liab"),
                DecrInventories = GetDecimal(item, fieldMap, "decr_inventories"),
                DecrOperPayable = GetDecimal(item, fieldMap, "decr_oper_payable"),
                IncrOperPayable = GetDecimal(item, fieldMap, "incr_oper_payable"),
                Others = GetDecimal(item, fieldMap, "others"),
                ImNetCashflowOperAct = GetDecimal(item, fieldMap, "im_net_cashflow_oper_act"),
                ConvDebtIntoCap = GetDecimal(item, fieldMap, "conv_debt_into_cap"),
                ConvCopbondsDueWithin1y = GetDecimal(item, fieldMap, "conv_copbonds_due_within_1y"),
                FaFncLeases = GetDecimal(item, fieldMap, "fa_fnc_leases"),
                ImNIncrCashEqu = GetDecimal(item, fieldMap, "im_n_incr_cash_equ"),
                NetDismCapitalAdd = GetDecimal(item, fieldMap, "net_dism_capital_add"),
                NetCashReceSec = GetDecimal(item, fieldMap, "net_cash_rece_sec"),
                CreditImpaLoss = GetDecimal(item, fieldMap, "credit_impa_loss"),
                UseRightAssetDep = GetDecimal(item, fieldMap, "use_right_asset_dep"),
                OthLossAsset = GetDecimal(item, fieldMap, "oth_loss_asset"),
                EndBalCash = GetDecimal(item, fieldMap, "end_bal_cash"),
                BegBalCash = GetDecimal(item, fieldMap, "beg_bal_cash"),
                EndBalCashEqu = GetDecimal(item, fieldMap, "end_bal_cash_equ"),
                BegBalCashEqu = GetDecimal(item, fieldMap, "beg_bal_cash_equ"),
                UpdateFlag = GetString(item, fieldMap, "update_flag"),
                
                UpdatedAt = DateTime.UtcNow
            };
            result.Add(statement);
        }

        return result;
    }

    // ==================== Upsert 方法 ====================

    private async Task UpsertStockDailiesAsync(List<StockDaily> dailies)
    {
        foreach (var daily in dailies)
        {
            var existing = await _context.StockDailies
                .FirstOrDefaultAsync(d => d.TsCode == daily.TsCode && d.TradeDate == daily.TradeDate);

            if (existing != null)
            {
                existing.Open = daily.Open;
                existing.High = daily.High;
                existing.Low = daily.Low;
                existing.Close = daily.Close;
                existing.PreClose = daily.PreClose;
                existing.Change = daily.Change;
                existing.PctChg = daily.PctChg;
                existing.Vol = daily.Vol;
                existing.Amount = daily.Amount;
                existing.UpdatedAt = DateTime.UtcNow;
            }
            else
            {
                _context.StockDailies.Add(daily);
            }
        }

        await _context.SaveChangesAsync();
    }

    private async Task UpsertIncomeStatementsAsync(List<IncomeStatement> statements)
    {
        // 去重：同一批次数据按 ts_code + end_date 只保留最后一条
        var deduped = statements
            .GroupBy(s => new { s.TsCode, s.EndDate })
            .Select(g => g.Last())
            .ToList();

        foreach (var statement in deduped)
        {
            var existing = await _context.IncomeStatements
                .FirstOrDefaultAsync(i => i.TsCode == statement.TsCode && i.EndDate == statement.EndDate);

            if (existing == null)
            {
                _context.IncomeStatements.Add(statement);
            }
            else
            {
                // 更新已存在记录 - 使用Entry方式更新所有字段
                _context.Entry(existing).CurrentValues.SetValues(statement);
                existing.Id = existing.Id; // 保持原有ID
                existing.UpdatedAt = DateTime.UtcNow;
            }
        }

        await _context.SaveChangesAsync();
    }

    private async Task UpsertBalanceSheetsAsync(List<BalanceSheet> sheets)
    {
        // 去重：同一批次数据按 ts_code + end_date 只保留最后一条
        var deduped = sheets
            .GroupBy(s => new { s.TsCode, s.EndDate })
            .Select(g => g.Last())
            .ToList();

        foreach (var sheet in deduped)
        {
            var existing = await _context.BalanceSheets
                .FirstOrDefaultAsync(b => b.TsCode == sheet.TsCode && b.EndDate == sheet.EndDate);

            if (existing == null)
            {
                _context.BalanceSheets.Add(sheet);
            }
            else
            {
                // 更新已存在记录 - 使用Entry方式更新所有字段
                _context.Entry(existing).CurrentValues.SetValues(sheet);
                existing.Id = existing.Id; // 保持原有ID
                existing.UpdatedAt = DateTime.UtcNow;
            }
        }

        await _context.SaveChangesAsync();
    }

    private async Task UpsertCashflowStatementsAsync(List<CashflowStatement> statements)
    {
        // 去重：同一批次数据按 ts_code + end_date 只保留最后一条
        var deduped = statements
            .GroupBy(s => new { s.TsCode, s.EndDate })
            .Select(g => g.Last())
            .ToList();

        foreach (var statement in deduped)
        {
            var existing = await _context.CashflowStatements
                .FirstOrDefaultAsync(c => c.TsCode == statement.TsCode && c.EndDate == statement.EndDate);

            if (existing == null)
            {
                _context.CashflowStatements.Add(statement);
            }
            else
            {
                // 更新已存在记录 - 使用Entry方式更新所有字段
                _context.Entry(existing).CurrentValues.SetValues(statement);
                existing.Id = existing.Id; // 保持原有ID
                existing.UpdatedAt = DateTime.UtcNow;
            }
        }

        await _context.SaveChangesAsync();
    }

    // ==================== 辅助方法 ====================

    private Dictionary<string, int> MapFields(List<string> fields)
    {
        var map = new Dictionary<string, int>();
        for (int i = 0; i < fields.Count; i++)
        {
            map[fields[i].ToLower()] = i;
        }
        return map;
    }

    private string? GetString(List<JsonElement> item, Dictionary<string, int> fieldMap, string fieldName)
    {
        if (!fieldMap.TryGetValue(fieldName.ToLower(), out var index) || index >= item.Count)
            return null;

        var element = item[index];
        if (element.ValueKind == JsonValueKind.Null || element.ValueKind == JsonValueKind.Undefined)
            return null;

        return element.GetString();
    }

    private DateOnly? GetDate(List<JsonElement> item, Dictionary<string, int> fieldMap, string fieldName)
    {
        var str = GetString(item, fieldMap, fieldName);
        if (string.IsNullOrEmpty(str)) return null;

        if (DateOnly.TryParseExact(str, "yyyyMMdd", out var date))
            return date;

        return null;
    }

    private decimal? GetDecimal(List<JsonElement> item, Dictionary<string, int> fieldMap, string fieldName)
    {
        if (!fieldMap.TryGetValue(fieldName.ToLower(), out var index) || index >= item.Count)
            return null;

        var element = item[index];
        if (element.ValueKind == JsonValueKind.Null || element.ValueKind == JsonValueKind.Undefined)
            return null;

        if (element.TryGetDecimal(out var value))
            return value;

        return null;
    }

    private async Task LogCallAsync(string appId, TushareQueryRequest request, string requestId, 
        DateTime startTime, int statusCode, string? errorMessage)
    {
        var paramsJson = JsonSerializer.Serialize(request.Params);
        var paramsHash = ComputeHash(paramsJson);

        var log = new CallLog
        {
            AppId = appId,
            ApiName = request.ApiName,
            ParamsHash = paramsHash,
            ParamsJson = paramsJson,
            RequestAt = startTime,
            DurationMs = (int)(DateTime.UtcNow - startTime).TotalMilliseconds,
            StatusCode = statusCode,
            RequestId = requestId,
            ErrorMessage = errorMessage
        };

        _context.CallLogs.Add(log);

        // 更新应用调用统计（仅成功调用计数）
        if (statusCode == 200)
        {
            var app = await _context.TushareApps.FirstOrDefaultAsync(a => a.AppId == appId);
            if (app != null)
            {
                app.CallCount++;
                app.LastUsedAt = DateTime.UtcNow;
                app.UpdatedAt = DateTime.UtcNow;
            }
        }

        await _context.SaveChangesAsync();
    }

    private string ComputeHash(string input)
    {
        using var md5 = MD5.Create();
        var bytes = Encoding.UTF8.GetBytes(input);
        var hash = md5.ComputeHash(bytes);
        return Convert.ToHexString(hash).ToLower();
    }
}
