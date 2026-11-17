-- 只读诊断脚本，不会终止会话、回滚事务或修改表。
-- 先切换到实际目标数据库。在“另一个查询窗口”运行，以观察正在等待的导入。
-- 将 NULL 改为导入脚本消息中显示的 SPID，可定位具体会话；不指定时查看当前库的活动请求。
DECLARE @TargetSessionId int = NULL;
SELECT DB_NAME() AS [当前数据库],@@SPID AS [本诊断窗口SPID],@@TRANCOUNT AS [本窗口事务数],XACT_STATE() AS [本窗口事务状态];

IF HAS_PERMS_BY_NAME(NULL,'SERVER','VIEW SERVER STATE')=1
BEGIN
    SELECT r.session_id AS [会话],r.status,r.command,r.wait_type,r.wait_time AS [等待毫秒],
           r.blocking_session_id AS [阻塞会话],r.wait_resource,r.total_elapsed_time AS [已执行毫秒],
           DB_NAME(r.database_id) AS [数据库],s.login_name,s.host_name,s.program_name,t.text AS [SQL文本]
    FROM sys.dm_exec_requests AS r
    JOIN sys.dm_exec_sessions AS s ON s.session_id=r.session_id
    OUTER APPLY sys.dm_exec_sql_text(r.sql_handle) AS t
    WHERE r.session_id<>@@SPID AND
          ((@TargetSessionId IS NULL AND r.database_id=DB_ID()) OR r.session_id=@TargetSessionId
            OR r.session_id IN (SELECT blocking_session_id FROM sys.dm_exec_requests WHERE session_id=@TargetSessionId));

    -- 睡眠会话也可能持有未提交事务，不能只检查正在运行的SQL。
    SELECT s.session_id,s.status,s.login_name,s.host_name,s.program_name,s.open_transaction_count,
           a.transaction_begin_time,a.transaction_type,a.transaction_state
    FROM sys.dm_exec_sessions AS s
    LEFT JOIN sys.dm_tran_session_transactions AS st ON st.session_id=s.session_id
    LEFT JOIN sys.dm_tran_active_transactions AS a ON a.transaction_id=st.transaction_id
    WHERE s.is_user_process=1 AND s.open_transaction_count>0;

    SELECT request_session_id,resource_type,resource_description,request_mode,request_status
    FROM sys.dm_tran_locks
    WHERE resource_database_id=DB_ID() AND (@TargetSessionId IS NULL OR request_session_id=@TargetSessionId
        OR request_session_id IN (SELECT blocking_session_id FROM sys.dm_exec_requests WHERE session_id=@TargetSessionId));
END
ELSE
    PRINT N'查看其他会话的完整阻塞信息需要 VIEW SERVER STATE，请让DBA运行本脚本；当前窗口事务数和下方表元数据仍可查看。';

-- 表的行数与索引元数据，不读取业务表的全部内容。
SELECT SUM(p.rows) AS [表行数估算] FROM sys.partitions AS p
WHERE p.object_id=OBJECT_ID(N'dbo.VoucherFeeRelation') AND p.index_id IN (0,1);
SELECT i.name AS [索引名],i.is_unique,ic.key_ordinal,c.name AS [字段],ic.is_included_column
FROM sys.indexes AS i
JOIN sys.index_columns AS ic ON ic.object_id=i.object_id AND ic.index_id=i.index_id
JOIN sys.columns AS c ON c.object_id=ic.object_id AND c.column_id=ic.column_id
WHERE i.object_id=OBJECT_ID(N'dbo.VoucherFeeRelation')
ORDER BY i.index_id,ic.key_ordinal,ic.index_column_id;
SELECT name AS [触发器],is_disabled FROM sys.triggers WHERE parent_id=OBJECT_ID(N'dbo.VoucherFeeRelation');

-- 解释：wait_type 为 LCK_M_* 且 blocking_session_id>0 时，表示正在等待对应会话释放锁。
-- 不要自动 KILL 阻塞会话。先由事务所属人员确认应提交还是回滚。
-- 若手动取消了导入，请回到“原导入窗口”单独执行：
-- SELECT @@SPID AS SessionId,@@TRANCOUNT AS OpenTransactions,XACT_STATE() AS TransactionState;
-- 若原窗口确实只用于本次测试且有遗留事务，经确认后可手工执行 ROLLBACK TRANSACTION。
