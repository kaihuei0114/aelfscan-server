using Nito.Collections;

namespace AElfScanServer.BlockChain.Provider;

public class TransactionRate
{
    public Deque<RateElement> Deque { get; set; } = new Deque<RateElement>();

    public void AddElement(RateElement element)
    {
        Deque.AddToBack(element);
    }
}

public class RateElement
{
    public int Count { get; set; }
    public long TimeStamp { get; set; }
}