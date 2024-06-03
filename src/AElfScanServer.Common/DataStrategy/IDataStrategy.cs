using System.Threading.Tasks;

namespace AElfScanServer.DataStrategy;

public interface IDataStrategy<TInput, TOutPut>
{
    Task LoadData(TInput input);
    Task<TOutPut> DisplayData(TInput input);
}

public class DataStrategyContext<TInput, TOutPut>
{
    private IDataStrategy<TInput, TOutPut> _dataStrategy;

    public DataStrategyContext(IDataStrategy<TInput, TOutPut> dataStrategy)
    {
        _dataStrategy = dataStrategy;
    }

    public async Task LoadData(TInput input)
    {
        await _dataStrategy.LoadData(input);
    }

    public async Task<TOutPut> DisplayData(TInput input)
    {
        return await _dataStrategy.DisplayData(input);
    }
}