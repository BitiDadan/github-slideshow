/********************现金流****************************/
DECLARE @cashFlowInserted int = 0, @cashFlowSkipped int = 0, @abstractInserted int = 0, @abstractSkipped int = 0;



--主账簿
IF EXISTS (SELECT 1 FROM [dbo].[VoucherAuxiliaryRelation] WITH (UPDLOCK,HOLDLOCK) WHERE [OverseasType]={{PRIMARY_TYPE}})
    SET @cashFlowSkipped += 9;
ELSE
BEGIN
    INSERT INTO dbo.VoucherAuxiliaryRelation(AuxiliaryCode,AuxiliaryName,AuxiliaryCodeOverseas,AuxiliaryNameOverseas,AuxiliaryJsonOverseas,OverseasType,Memo)VALUES('0017','客商','客户','客户','[''客户'':[''number'':''{0}'']]',{{PRIMARY_TYPE}},'')
    SET @cashFlowInserted += @@ROWCOUNT;
    INSERT INTO dbo.VoucherAuxiliaryRelation(AuxiliaryCode,AuxiliaryName,AuxiliaryCodeOverseas,AuxiliaryNameOverseas,AuxiliaryJsonOverseas,OverseasType,Memo)VALUES('0017','客商','供应商','供应商','[''供应商":[''number":''{0}'']]',{{PRIMARY_TYPE}},'')
    SET @cashFlowInserted += @@ROWCOUNT;
    INSERT INTO dbo.VoucherAuxiliaryRelation(AuxiliaryCode,AuxiliaryName,AuxiliaryCodeOverseas,AuxiliaryNameOverseas,AuxiliaryJsonOverseas,OverseasType,Memo)VALUES('bondex-syb','事业部','部门','部门','[''部门":[''number":''{0}'']]',{{PRIMARY_TYPE}},'')
    SET @cashFlowInserted += @@ROWCOUNT;
    INSERT INTO dbo.VoucherAuxiliaryRelation(AuxiliaryCode,AuxiliaryName,AuxiliaryCodeOverseas,AuxiliaryNameOverseas,AuxiliaryJsonOverseas,OverseasType,Memo)VALUES('0001','部门','部门','部门','[''部门":[''number":''{0}'']]',{{PRIMARY_TYPE}},'')
    SET @cashFlowInserted += @@ROWCOUNT;
    INSERT INTO dbo.VoucherAuxiliaryRelation(AuxiliaryCode,AuxiliaryName,AuxiliaryCodeOverseas,AuxiliaryNameOverseas,AuxiliaryJsonOverseas,OverseasType,Memo)VALUES('0011','银行账户','银行账户','银行账户','[''银行账户":[''number":''{0}'']]',{{PRIMARY_TYPE}},'')
    SET @cashFlowInserted += @@ROWCOUNT;
    INSERT INTO dbo.VoucherAuxiliaryRelation(AuxiliaryCode,AuxiliaryName,AuxiliaryCodeOverseas,AuxiliaryNameOverseas,AuxiliaryJsonOverseas,OverseasType,Memo)VALUES('bondex-ry','人员','人员','人员','[''人员":[''number":''{0}'']]',{{PRIMARY_TYPE}},'')
    SET @cashFlowInserted += @@ROWCOUNT;
    INSERT INTO dbo.VoucherAuxiliaryRelation(AuxiliaryCode,AuxiliaryName,AuxiliaryCodeOverseas,AuxiliaryNameOverseas,AuxiliaryJsonOverseas,OverseasType,Memo)VALUES('bondex-kxxz','款项性质','款项性质','款项性质','[''款项性质":[''number":''{0}'']]',{{PRIMARY_TYPE}},'')
    SET @cashFlowInserted += @@ROWCOUNT;
    INSERT INTO dbo.VoucherAuxiliaryRelation(AuxiliaryCode,AuxiliaryName,AuxiliaryCodeOverseas,AuxiliaryNameOverseas,AuxiliaryJsonOverseas,OverseasType,Memo)VALUES('bondex-ywlx','业务类型','业务类型','业务类型','[''业务类型":[''number":''{0}'']]',{{PRIMARY_TYPE}},'')
    SET @cashFlowInserted += @@ROWCOUNT;
    INSERT INTO dbo.VoucherAuxiliaryRelation(AuxiliaryCode,AuxiliaryName,AuxiliaryCodeOverseas,AuxiliaryNameOverseas,AuxiliaryJsonOverseas,OverseasType,Memo)VALUES('0007','现金流量','现金流量','现金流量','[{"key":"1111","value":"CFI-IFRS-01.01.01"},{"key":"1121","value":"CFI-IFRS-01.02.01"},{"key":"111310","value":"CFI-IFRS-01.01.03.07"},{"key":"111313","value":"CFI-IFRS-01.01.03.10"},{"key":"111314","value":"CFI-IFRS-01.01.03.11"},{"key":"112410","value":"CFI-IFRS-01.02.04.08"},{"key":"112413","value":"CFI-IFRS-01.02.04.11"},{"key":"112414","value":"CFI-IFRS-01.02.04.12"}]',{{PRIMARY_TYPE}},'')
    SET @cashFlowInserted += @@ROWCOUNT;
END;


--副账簿
IF EXISTS (SELECT 1 FROM [dbo].[VoucherAuxiliaryRelation] WITH (UPDLOCK,HOLDLOCK) WHERE [OverseasType]={{SECONDARY_TYPE}})
    SET @cashFlowSkipped += 9;
ELSE
BEGIN
    INSERT INTO dbo.VoucherAuxiliaryRelation(AuxiliaryCode,AuxiliaryName,AuxiliaryCodeOverseas,AuxiliaryNameOverseas,AuxiliaryJsonOverseas,OverseasType,Memo)VALUES('0017','客商','客户','客户','[''客户'':[''number'':''{0}'']]',{{SECONDARY_TYPE}},'')
    SET @cashFlowInserted += @@ROWCOUNT;
    INSERT INTO dbo.VoucherAuxiliaryRelation(AuxiliaryCode,AuxiliaryName,AuxiliaryCodeOverseas,AuxiliaryNameOverseas,AuxiliaryJsonOverseas,OverseasType,Memo)VALUES('0017','客商','供应商','供应商','[''供应商":[''number":''{0}'']]',{{SECONDARY_TYPE}},'')
    SET @cashFlowInserted += @@ROWCOUNT;
    INSERT INTO dbo.VoucherAuxiliaryRelation(AuxiliaryCode,AuxiliaryName,AuxiliaryCodeOverseas,AuxiliaryNameOverseas,AuxiliaryJsonOverseas,OverseasType,Memo)VALUES('bondex-syb','事业部','部门','部门','[''部门":[''number":''{0}'']]',{{SECONDARY_TYPE}},'')
    SET @cashFlowInserted += @@ROWCOUNT;
    INSERT INTO dbo.VoucherAuxiliaryRelation(AuxiliaryCode,AuxiliaryName,AuxiliaryCodeOverseas,AuxiliaryNameOverseas,AuxiliaryJsonOverseas,OverseasType,Memo)VALUES('0001','部门','部门','部门','[''部门":[''number":''{0}'']]',{{SECONDARY_TYPE}},'')
    SET @cashFlowInserted += @@ROWCOUNT;
    INSERT INTO dbo.VoucherAuxiliaryRelation(AuxiliaryCode,AuxiliaryName,AuxiliaryCodeOverseas,AuxiliaryNameOverseas,AuxiliaryJsonOverseas,OverseasType,Memo)VALUES('0011','银行账户','银行账户','银行账户','[''银行账户":[''number":''{0}'']]',{{SECONDARY_TYPE}},'')
    SET @cashFlowInserted += @@ROWCOUNT;
    INSERT INTO dbo.VoucherAuxiliaryRelation(AuxiliaryCode,AuxiliaryName,AuxiliaryCodeOverseas,AuxiliaryNameOverseas,AuxiliaryJsonOverseas,OverseasType,Memo)VALUES('bondex-ry','人员','人员','人员','[''人员":[''number":''{0}'']]',{{SECONDARY_TYPE}},'')
    SET @cashFlowInserted += @@ROWCOUNT;
    INSERT INTO dbo.VoucherAuxiliaryRelation(AuxiliaryCode,AuxiliaryName,AuxiliaryCodeOverseas,AuxiliaryNameOverseas,AuxiliaryJsonOverseas,OverseasType,Memo)VALUES('bondex-kxxz','款项性质','款项性质','款项性质','[''款项性质":[''number":''{0}'']]',{{SECONDARY_TYPE}},'')
    SET @cashFlowInserted += @@ROWCOUNT;
    INSERT INTO dbo.VoucherAuxiliaryRelation(AuxiliaryCode,AuxiliaryName,AuxiliaryCodeOverseas,AuxiliaryNameOverseas,AuxiliaryJsonOverseas,OverseasType,Memo)VALUES('bondex-ywlx','业务类型','业务类型','业务类型','[''业务类型":[''number":''{0}'']]',{{SECONDARY_TYPE}},'')
    SET @cashFlowInserted += @@ROWCOUNT;
    INSERT INTO dbo.VoucherAuxiliaryRelation(AuxiliaryCode,AuxiliaryName,AuxiliaryCodeOverseas,AuxiliaryNameOverseas,AuxiliaryJsonOverseas,OverseasType,Memo)VALUES('0007','现金流量','现金流量','现金流量','[{"key":"1111","value":"CI01.01.01"},{"key":"1121","value":"CI01.02.01"},{"key":"111310","value":"CI01.01.03.07"},{"key":"111313","value":"CI01.01.03.10"},{"key":"111314","value":"CI01.01.03.11"},{"key":"112410","value":"CI01.02.04.09"},{"key":"112413","value":"CI01.02.04.12"},{"key":"112414","value":"CI01.02.04.13"}]',{{SECONDARY_TYPE}},'')
    SET @cashFlowInserted += @@ROWCOUNT;
END;








/********************凭证摘要-VoucherAbstractRelation****************************/

--主账簿
IF EXISTS (SELECT 1 FROM [dbo].[VoucherAbstractRelation] WITH (UPDLOCK,HOLDLOCK) WHERE [OverseasType]={{PRIMARY_TYPE}})
    SET @abstractSkipped += 3;
ELSE
BEGIN
    INSERT INTO dbo.VoucherAbstractRelation(OriginalAuxiliary, TranslateAuxiliary, OverseasType, LanguageType, Memo, CreateDate, IsDeleted) VALUES('收运费',N'collect freight charges',{{PRIMARY_TYPE}},{{CURRENCY}},'',getdate(),0)
    SET @abstractInserted += @@ROWCOUNT;
    INSERT INTO dbo.VoucherAbstractRelation(OriginalAuxiliary, TranslateAuxiliary, OverseasType, LanguageType, Memo, CreateDate, IsDeleted) VALUES('付运费',N'pay freight',{{PRIMARY_TYPE}},{{CURRENCY}},'',getdate(),0)
    SET @abstractInserted += @@ROWCOUNT;
    INSERT INTO dbo.VoucherAbstractRelation(OriginalAuxiliary, TranslateAuxiliary, OverseasType, LanguageType, Memo, CreateDate, IsDeleted) VALUES('预收运费',N'receive in advance',{{PRIMARY_TYPE}},{{CURRENCY}},'',getdate(),0)
    SET @abstractInserted += @@ROWCOUNT;
END;

--副账簿 默认CNY 不需要配置
