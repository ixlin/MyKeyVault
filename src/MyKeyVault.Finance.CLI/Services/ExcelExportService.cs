using OfficeOpenXml;
using System.Text.Json;

namespace MyKeyVault.Finance.CLI.Services;

public class ExcelExportService
{
    public ExcelExportService()
    {
        // EPPlus 8 不再需要在代码中设置许可证，而是在创建 ExcelPackage 时传递参数
    }

    /// <summary>
    /// 导出三大财报到 Excel
    /// </summary>
    public void ExportFinancialStatements(
        string filePath,
        JsonElement? balanceSheetData,
        JsonElement? incomeStatementData,
        JsonElement? cashflowData)
    {
        // EPPlus 8 使用新的方式设置许可证
        using var package = new ExcelPackage(new FileInfo(filePath));

        // 创建三个 Sheet
        if (balanceSheetData.HasValue && balanceSheetData.Value.ValueKind != JsonValueKind.Null)
        {
            CreateBalanceSheetWorksheet(package, balanceSheetData.Value);
        }

        if (incomeStatementData.HasValue && incomeStatementData.Value.ValueKind != JsonValueKind.Null)
        {
            CreateIncomeStatementWorksheet(package, incomeStatementData.Value);
        }

        if (cashflowData.HasValue && cashflowData.Value.ValueKind != JsonValueKind.Null)
        {
            CreateCashflowWorksheet(package, cashflowData.Value);
        }

        // 保存文件
        package.Save();
    }

    private void CreateBalanceSheetWorksheet(ExcelPackage package, JsonElement data)
    {
        var worksheet = package.Workbook.Worksheets.Add("资产负债表");
        
        // 定义字段映射 (API字段名 -> 中文名称)
        var fieldMappings = new Dictionary<string, string>
        {
            { "ts_code", "股票代码" },
            { "ann_date", "公告日期" },
            { "f_ann_date", "实际公告日期" },
            { "end_date", "报告期" },
            { "report_type", "报表类型" },
            { "comp_type", "公司类型" },
            { "end_type", "报告期类型" },
            { "total_share", "期末总股本" },
            { "cap_rese", "资本公积金" },
            { "undistr_porfit", "未分配利润" },
            { "surplus_rese", "盈余公积金" },
            { "special_rese", "专项储备" },
            { "money_cap", "货币资金" },
            { "trad_asset", "交易性金融资产" },
            { "notes_receiv", "应收票据" },
            { "accounts_receiv", "应收账款" },
            { "oth_receiv", "其他应收款" },
            { "prepayment", "预付款项" },
            { "div_receiv", "应收股利" },
            { "int_receiv", "应收利息" },
            { "inventories", "存货" },
            { "amor_exp", "待摊费用" },
            { "nca_within_1y", "一年内到期的非流动资产" },
            { "sett_rsrv", "结算备付金" },
            { "loanto_oth_bank_fi", "拆出资金" },
            { "premium_receiv", "应收保费" },
            { "reinsur_receiv", "应收分保账款" },
            { "reinsur_res_receiv", "应收分保合同准备金" },
            { "pur_resale_fa", "买入返售金融资产" },
            { "oth_cur_assets", "其他流动资产" },
            { "total_cur_assets", "流动资产合计" },
            { "fa_avail_for_sale", "可供出售金融资产" },
            { "htm_invest", "持有至到期投资" },
            { "lt_eqt_invest", "长期股权投资" },
            { "invest_real_estate", "投资性房地产" },
            { "time_deposits", "定期存款" },
            { "oth_assets", "其他资产" },
            { "lt_rec", "长期应收款" },
            { "fix_assets", "固定资产" },
            { "cip", "在建工程" },
            { "const_materials", "工程物资" },
            { "fixed_assets_disp", "固定资产清理" },
            { "produc_bio_assets", "生产性生物资产" },
            { "oil_and_gas_assets", "油气资产" },
            { "intan_assets", "无形资产" },
            { "r_and_d", "研发支出" },
            { "goodwill", "商誉" },
            { "lt_amor_exp", "长期待摊费用" },
            { "defer_tax_assets", "递延所得税资产" },
            { "decr_in_disbur", "发放贷款及垫款" },
            { "oth_nca", "其他非流动资产" },
            { "total_nca", "非流动资产合计" },
            { "total_assets", "资产总计" },
            { "total_liab", "负债合计" },
            { "total_hldr_eqy_exc_min_int", "股东权益合计(不含少数股东权益)" },
            { "total_hldr_eqy_inc_min_int", "股东权益合计(含少数股东权益)" }
        };

        WriteDataToWorksheet(worksheet, data, fieldMappings);
    }

    private void CreateIncomeStatementWorksheet(ExcelPackage package, JsonElement data)
    {
        var worksheet = package.Workbook.Worksheets.Add("利润表");
        
        var fieldMappings = new Dictionary<string, string>
        {
            { "ts_code", "股票代码" },
            { "ann_date", "公告日期" },
            { "f_ann_date", "实际公告日期" },
            { "end_date", "报告期" },
            { "report_type", "报表类型" },
            { "comp_type", "公司类型" },
            { "end_type", "报告期类型" },
            { "basic_eps", "基本每股收益" },
            { "diluted_eps", "稀释每股收益" },
            { "total_revenue", "营业总收入" },
            { "revenue", "营业收入" },
            { "int_income", "利息收入" },
            { "prem_earned", "已赚保费" },
            { "comm_income", "手续费及佣金收入" },
            { "n_commis_income", "手续费及佣金净收入" },
            { "n_oth_income", "其他经营净收益" },
            { "n_oth_b_income", "其他业务净收益" },
            { "prem_income", "保险业务收入" },
            { "out_prem", "分出保费" },
            { "une_prem_reser", "提取未到期责任准备金" },
            { "reins_income", "分保费收入" },
            { "n_sec_tb_income", "代理买卖证券业务净收入" },
            { "n_sec_uw_income", "证券承销业务净收入" },
            { "n_asset_mg_income", "受托客户资产管理业务净收入" },
            { "oth_b_income", "其他业务收入" },
            { "fv_value_chg_gain", "公允价值变动净收益" },
            { "invest_income", "投资净收益" },
            { "ass_invest_income", "对联营企业和合营企业的投资收益" },
            { "forex_gain", "汇兑净收益" },
            { "total_cogs", "营业总成本" },
            { "oper_cost", "营业成本" },
            { "int_exp", "利息支出" },
            { "comm_exp", "手续费及佣金支出" },
            { "biz_tax_surchg", "营业税金及附加" },
            { "sell_exp", "销售费用" },
            { "admin_exp", "管理费用" },
            { "fin_exp", "财务费用" },
            { "assets_impair_loss", "资产减值损失" },
            { "prem_refund", "退保金" },
            { "compens_payout", "赔付总支出" },
            { "reser_insur_liab", "提取保险责任准备金" },
            { "div_payt", "保户红利支出" },
            { "reins_exp", "分保费用" },
            { "oper_exp", "营业支出" },
            { "compens_payout_refu", "摊回赔付支出" },
            { "insur_reser_refu", "摊回保险责任准备金" },
            { "reins_cost_refund", "摊回分保费用" },
            { "other_bus_cost", "其他业务成本" },
            { "operate_profit", "营业利润" },
            { "non_oper_income", "营业外收入" },
            { "non_oper_exp", "营业外支出" },
            { "nca_disploss", "非流动资产处置净损失" },
            { "total_profit", "利润总额" },
            { "income_tax", "所得税费用" },
            { "n_income", "净利润(含少数股东损益)" },
            { "n_income_attr_p", "净利润(不含少数股东损益)" },
            { "minority_gain", "少数股东损益" },
            { "oth_compr_income", "其他综合收益" },
            { "t_compr_income", "综合收益总额" },
            { "compr_inc_attr_p", "归属于母公司的综合收益总额" }
        };

        WriteDataToWorksheet(worksheet, data, fieldMappings);
    }

    private void CreateCashflowWorksheet(ExcelPackage package, JsonElement data)
    {
        var worksheet = package.Workbook.Worksheets.Add("现金流量表");
        
        var fieldMappings = new Dictionary<string, string>
        {
            { "ts_code", "股票代码" },
            { "ann_date", "公告日期" },
            { "f_ann_date", "实际公告日期" },
            { "end_date", "报告期" },
            { "comp_type", "公司类型" },
            { "report_type", "报表类型" },
            { "end_type", "报告期类型" },
            { "net_profit", "净利润" },
            { "finan_exp", "财务费用" },
            { "c_fr_sale_sg", "销售商品、提供劳务收到的现金" },
            { "recp_tax_rends", "收到的税费返还" },
            { "n_depos_incr_fi", "客户存款和同业存放款项净增加额" },
            { "n_incr_loans_cb", "向中央银行借款净增加额" },
            { "n_inc_borr_oth_fi", "向其他金融机构拆入资金净增加额" },
            { "prem_fr_orig_contr", "收到原保险合同保费取得的现金" },
            { "n_incr_insured_dep", "保户储金净增加额" },
            { "n_reinsur_prem", "收到再保业务现金净额" },
            { "n_incr_disp_tfa", "处置交易性金融资产净增加额" },
            { "ifc_cash_incr", "收取利息和手续费净增加额" },
            { "n_incr_disp_faas", "处置可供出售金融资产净增加额" },
            { "n_incr_loans_oth_bank", "拆入资金净增加额" },
            { "n_cap_incr_repur", "回购业务资金净增加额" },
            { "c_fr_oth_operate_a", "收到其他与经营活动有关的现金" },
            { "c_inf_fr_operate_a", "经营活动现金流入小计" },
            { "c_paid_goods_s", "购买商品、接受劳务支付的现金" },
            { "c_paid_to_for_empl", "支付给职工以及为职工支付的现金" },
            { "c_paid_for_taxes", "支付的各项税费" },
            { "n_incr_clt_loan_adv", "客户贷款及垫款净增加额" },
            { "n_incr_dep_cbob", "存放央行和同业款项净增加额" },
            { "c_pay_claims_orig_inco", "支付原保险合同赔付款项的现金" },
            { "pay_handling_chrg", "支付手续费的现金" },
            { "pay_comm_insur_plcy", "支付保单红利的现金" },
            { "oth_cash_pay_oper_act", "支付其他与经营活动有关的现金" },
            { "st_cash_out_act", "经营活动现金流出小计" },
            { "n_cashflow_act", "经营活动产生的现金流量净额" },
            { "oth_recp_ral_inv_act", "收到其他与投资活动有关的现金" },
            { "c_disp_withdrwl_invest", "收回投资收到的现金" },
            { "c_recp_return_invest", "取得投资收益收到的现金" },
            { "n_recp_disp_filta", "处置固定资产、无形资产和其他长期资产收回的现金净额" },
            { "n_recp_disp_sobu", "处置子公司及其他营业单位收到的现金净额" },
            { "stot_inflows_inv_act", "投资活动现金流入小计" },
            { "c_pay_acq_const_filta", "购建固定资产、无形资产和其他长期资产支付的现金" },
            { "c_paid_invest", "投资支付的现金" },
            { "n_disp_subs_oth_biz", "取得子公司及其他营业单位支付的现金净额" },
            { "oth_pay_ral_inv_act", "支付其他与投资活动有关的现金" },
            { "n_incr_pledge_loan", "质押贷款净增加额" },
            { "stot_out_inv_act", "投资活动现金流出小计" },
            { "n_cashflow_inv_act", "投资活动产生的现金流量净额" },
            { "c_recp_borrow", "取得借款收到的现金" },
            { "proc_issue_bonds", "发行债券收到的现金" },
            { "oth_cash_recp_ral_fnc_act", "收到其他与筹资活动有关的现金" },
            { "stot_cash_in_fnc_act", "筹资活动现金流入小计" },
            { "free_cashflow", "企业自由现金流量" },
            { "c_prepay_amt_borr", "偿还债务支付的现金" },
            { "c_pay_dist_dpcp_int_exp", "分配股利、利润或偿付利息支付的现金" },
            { "incl_dvd_profit_paid_sc_ms", "子公司支付给少数股东的股利、利润" },
            { "oth_cashpay_ral_fnc_act", "支付其他与筹资活动有关的现金" },
            { "stot_cashout_fnc_act", "筹资活动现金流出小计" },
            { "n_cash_flows_fnc_act", "筹资活动产生的现金流量净额" },
            { "eff_fx_flu_cash", "汇率变动对现金的影响" },
            { "n_incr_cash_cash_equ", "现金及现金等价物净增加额" },
            { "c_cash_equ_beg_period", "期初现金及现金等价物余额" },
            { "c_cash_equ_end_period", "期末现金及现金等价物余额" }
        };

        WriteDataToWorksheet(worksheet, data, fieldMappings);
    }

    private void WriteDataToWorksheet(ExcelWorksheet worksheet, JsonElement data, Dictionary<string, string> fieldMappings)
    {
        // 检查数据是否为数组
        if (data.ValueKind != JsonValueKind.Array)
        {
            worksheet.Cells[1, 1].Value = "数据格式错误";
            return;
        }

        var dataArray = data.EnumerateArray().ToList();
        if (dataArray.Count == 0)
        {
            worksheet.Cells[1, 1].Value = "无数据";
            return;
        }

        // 获取第一条数据来确定列
        var firstItem = dataArray[0];
        var columns = new List<(string key, string name)>();

        // 根据第一条数据的字段和映射表生成列
        foreach (var property in firstItem.EnumerateObject())
        {
            var key = property.Name;
            var name = fieldMappings.ContainsKey(key) ? fieldMappings[key] : key;
            columns.Add((key, name));
        }

        // 写入表头
        for (int i = 0; i < columns.Count; i++)
        {
            worksheet.Cells[1, i + 1].Value = columns[i].name;
            worksheet.Cells[1, i + 1].Style.Font.Bold = true;
        }

        // 写入数据
        int row = 2;
        foreach (var item in dataArray)
        {
            for (int col = 0; col < columns.Count; col++)
            {
                var key = columns[col].key;
                if (item.TryGetProperty(key, out var value))
                {
                    worksheet.Cells[row, col + 1].Value = GetValueAsString(value);
                }
            }
            row++;
        }

        // 自动调整列宽
        worksheet.Cells[worksheet.Dimension.Address].AutoFitColumns();
    }

    private string? GetValueAsString(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Number => element.GetDecimal().ToString(),
            JsonValueKind.True => "是",
            JsonValueKind.False => "否",
            JsonValueKind.Null => null,
            _ => element.ToString()
        };
    }
}
