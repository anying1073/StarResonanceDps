namespace StarResonanceDpsAnalysis.Core.Analyze;

public interface IMessageAnalyzer
{
    /// <summary>
    /// 主入口：处理一批TCP数据包
    /// </summary>
    void Process(byte[] packets);
}