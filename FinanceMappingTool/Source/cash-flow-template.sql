/********************现金流****************************/



--主账簿
INSERT INTO dbo.VoucherAuxiliaryRelation(AuxiliaryCode,AuxiliaryName,AuxiliaryCodeOverseas,AuxiliaryNameOverseas,AuxiliaryJsonOverseas,OverseasType,Memo)VALUES('0017','客商','客户','客户','[''客户'':[''number'':''{0}'']]',{{PRIMARY_TYPE}},'')
INSERT INTO dbo.VoucherAuxiliaryRelation(AuxiliaryCode,AuxiliaryName,AuxiliaryCodeOverseas,AuxiliaryNameOverseas,AuxiliaryJsonOverseas,OverseasType,Memo)VALUES('0017','客商','供应商','供应商','[''供应商":[''number":''{0}'']]',{{PRIMARY_TYPE}},'')
INSERT INTO dbo.VoucherAuxiliaryRelation(AuxiliaryCode,AuxiliaryName,AuxiliaryCodeOverseas,AuxiliaryNameOverseas,AuxiliaryJsonOverseas,OverseasType,Memo)VALUES('bondex-syb','事业部','部门','部门','[''部门":[''number":''{0}'']]',{{PRIMARY_TYPE}},'')
INSERT INTO dbo.VoucherAuxiliaryRelation(AuxiliaryCode,AuxiliaryName,AuxiliaryCodeOverseas,AuxiliaryNameOverseas,AuxiliaryJsonOverseas,OverseasType,Memo)VALUES('0001','部门','部门','部门','[''部门":[''number":''{0}'']]',{{PRIMARY_TYPE}},'')
INSERT INTO dbo.VoucherAuxiliaryRelation(AuxiliaryCode,AuxiliaryName,AuxiliaryCodeOverseas,AuxiliaryNameOverseas,AuxiliaryJsonOverseas,OverseasType,Memo)VALUES('0011','银行账户','银行账户','银行账户','[''银行账户":[''number":''{0}'']]',{{PRIMARY_TYPE}},'')
INSERT INTO dbo.VoucherAuxiliaryRelation(AuxiliaryCode,AuxiliaryName,AuxiliaryCodeOverseas,AuxiliaryNameOverseas,AuxiliaryJsonOverseas,OverseasType,Memo)VALUES('bondex-ry','人员','人员','人员','[''人员":[''number":''{0}'']]',{{PRIMARY_TYPE}},'')
INSERT INTO dbo.VoucherAuxiliaryRelation(AuxiliaryCode,AuxiliaryName,AuxiliaryCodeOverseas,AuxiliaryNameOverseas,AuxiliaryJsonOverseas,OverseasType,Memo)VALUES('bondex-kxxz','款项性质','款项性质','款项性质','[''款项性质":[''number":''{0}'']]',{{PRIMARY_TYPE}},'')
INSERT INTO dbo.VoucherAuxiliaryRelation(AuxiliaryCode,AuxiliaryName,AuxiliaryCodeOverseas,AuxiliaryNameOverseas,AuxiliaryJsonOverseas,OverseasType,Memo)VALUES('bondex-ywlx','业务类型','业务类型','业务类型','[''业务类型":[''number":''{0}'']]',{{PRIMARY_TYPE}},'')
INSERT INTO dbo.VoucherAuxiliaryRelation(AuxiliaryCode,AuxiliaryName,AuxiliaryCodeOverseas,AuxiliaryNameOverseas,AuxiliaryJsonOverseas,OverseasType,Memo)VALUES('0007','现金流量','现金流量','现金流量','[{"key":"1111","value":"CI01.01.01"},{"key":"1121","value":"CI01.02.01"},{"key":"111310","value":"CI01.01.03.07"},{"key":"111313","value":"CI01.01.03.10"},{"key":"111314","value":"CI01.01.03.11"},{"key":"112410","value":"CI01.02.04.09"},{"key":"112413","value":"CI01.02.04.12"},{"key":"112414","value":"CI01.02.04.13"}]',{{PRIMARY_TYPE}},'')


--副账簿
INSERT INTO dbo.VoucherAuxiliaryRelation(AuxiliaryCode,AuxiliaryName,AuxiliaryCodeOverseas,AuxiliaryNameOverseas,AuxiliaryJsonOverseas,OverseasType,Memo)VALUES('0017','客商','客户','客户','[''客户'':[''number'':''{0}'']]',{{SECONDARY_TYPE}},'')
INSERT INTO dbo.VoucherAuxiliaryRelation(AuxiliaryCode,AuxiliaryName,AuxiliaryCodeOverseas,AuxiliaryNameOverseas,AuxiliaryJsonOverseas,OverseasType,Memo)VALUES('0017','客商','供应商','供应商','[''供应商":[''number":''{0}'']]',{{SECONDARY_TYPE}},'')
INSERT INTO dbo.VoucherAuxiliaryRelation(AuxiliaryCode,AuxiliaryName,AuxiliaryCodeOverseas,AuxiliaryNameOverseas,AuxiliaryJsonOverseas,OverseasType,Memo)VALUES('bondex-syb','事业部','部门','部门','[''部门":[''number":''{0}'']]',{{SECONDARY_TYPE}},'')
INSERT INTO dbo.VoucherAuxiliaryRelation(AuxiliaryCode,AuxiliaryName,AuxiliaryCodeOverseas,AuxiliaryNameOverseas,AuxiliaryJsonOverseas,OverseasType,Memo)VALUES('0001','部门','部门','部门','[''部门":[''number":''{0}'']]',{{SECONDARY_TYPE}},'')
INSERT INTO dbo.VoucherAuxiliaryRelation(AuxiliaryCode,AuxiliaryName,AuxiliaryCodeOverseas,AuxiliaryNameOverseas,AuxiliaryJsonOverseas,OverseasType,Memo)VALUES('0011','银行账户','银行账户','银行账户','[''银行账户":[''number":''{0}'']]',{{SECONDARY_TYPE}},'')
INSERT INTO dbo.VoucherAuxiliaryRelation(AuxiliaryCode,AuxiliaryName,AuxiliaryCodeOverseas,AuxiliaryNameOverseas,AuxiliaryJsonOverseas,OverseasType,Memo)VALUES('bondex-ry','人员','人员','人员','[''人员":[''number":''{0}'']]',{{SECONDARY_TYPE}},'')
INSERT INTO dbo.VoucherAuxiliaryRelation(AuxiliaryCode,AuxiliaryName,AuxiliaryCodeOverseas,AuxiliaryNameOverseas,AuxiliaryJsonOverseas,OverseasType,Memo)VALUES('bondex-kxxz','款项性质','款项性质','款项性质','[''款项性质":[''number":''{0}'']]',{{SECONDARY_TYPE}},'')
INSERT INTO dbo.VoucherAuxiliaryRelation(AuxiliaryCode,AuxiliaryName,AuxiliaryCodeOverseas,AuxiliaryNameOverseas,AuxiliaryJsonOverseas,OverseasType,Memo)VALUES('bondex-ywlx','业务类型','业务类型','业务类型','[''业务类型":[''number":''{0}'']]',{{SECONDARY_TYPE}},'')
INSERT INTO dbo.VoucherAuxiliaryRelation(AuxiliaryCode,AuxiliaryName,AuxiliaryCodeOverseas,AuxiliaryNameOverseas,AuxiliaryJsonOverseas,OverseasType,Memo)VALUES('0007','现金流量','现金流量','现金流量','[{"key":"1111","value":"CI01.01.01"},{"key":"1121","value":"CI01.02.01"},{"key":"111310","value":"CI01.01.03.07"},{"key":"111313","value":"CI01.01.03.10"},{"key":"111314","value":"CI01.01.03.11"},{"key":"112410","value":"CI01.02.04.09"},{"key":"112413","value":"CI01.02.04.12"},{"key":"112414","value":"CI01.02.04.13"}]',{{SECONDARY_TYPE}},'')








/********************凭证摘要-VoucherAbstractRelation****************************/

--主账簿
INSERT INTO dbo.VoucherAbstractRelation(OriginalAuxiliary, TranslateAuxiliary, OverseasType, LanguageType, Memo, CreateDate, IsDeleted) VALUES('收运费',N'collect freight charges',{{PRIMARY_TYPE}},{{CURRENCY}},'',getdate(),0)
INSERT INTO dbo.VoucherAbstractRelation(OriginalAuxiliary, TranslateAuxiliary, OverseasType, LanguageType, Memo, CreateDate, IsDeleted) VALUES('付运费',N'Pay freight',{{PRIMARY_TYPE}},{{CURRENCY}},'',getdate(),0)
INSERT INTO dbo.VoucherAbstractRelation(OriginalAuxiliary, TranslateAuxiliary, OverseasType, LanguageType, Memo, CreateDate, IsDeleted) VALUES('预收运费',N'receive in advance',{{PRIMARY_TYPE}},{{CURRENCY}},'',getdate(),0)

--副账簿 默认CNY 不需要配置
