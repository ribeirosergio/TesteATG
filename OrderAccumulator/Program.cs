using QuickFix;
using QuickFix.Fields;
using QuickFix.FIX44;

namespace OrderAccumulatorMicroservice
{
    class Program
    {
        static void Main(string[] args)
        {
            SessionSettings settings = new("orderaccumulator.cfg");
            IMessageStoreFactory storeFactory = new FileStoreFactory(settings);
            ILogFactory logFactory = new FileLogFactory(settings);
            IMessageFactory messageFactory = new DefaultMessageFactory();

            OrderAccumulatorApplication application = new();

            ThreadedSocketAcceptor acceptor = new(application, storeFactory, settings, logFactory, messageFactory);
            acceptor.Start();

            Console.WriteLine("Pressione qualquer tecla para encerrar.");

            acceptor.Stop();
        }
    }

    class OrderAccumulatorApplication : MessageCracker, IApplication
    {
        Session? _session;

        decimal limiteExposicao = 1000000;
        decimal exposicaoAtual = 0;
        int orderID = 0;
        int execID = 0;
        int origClOrdID = 0;

        private string GenOrderID() { return (++orderID).ToString(); }
        private string GenExecID() { return (++execID).ToString(); }
        private string GenOrigClOrdID() { return (++origClOrdID).ToString(); }

        public void FromApp(QuickFix.Message message, SessionID sessionId) { }
        public void OnCreate(SessionID sessionId) { }
        public void OnLogon(SessionID sessionId) { _session = Session.LookupSession(sessionId); }
        public void OnLogout(SessionID sessionId) { }
        public void ToAdmin(QuickFix.Message message, SessionID sessionId) { }
        public void ToApp(QuickFix.Message message, SessionID sessionId) { }
        public void FromAdmin(QuickFix.Message message, SessionID sessionId) { }

        public void OnMessage(NewOrderSingle order, SessionID sessionID)
        {
            decimal orderValue = order.OrderQty.getValue() * order.Price.getValue();

            if (order.Side.getValue() == Side.BUY)
            {
                if (exposicaoAtual + orderValue <= limiteExposicao)
                {
                    exposicaoAtual += orderValue;
                    ExecutionReport executionReport = new(
                        new OrderID(GenOrderID()),
                        new ExecID(GenExecID()), 
                        new ExecType(ExecType.FILL), 
                        new OrdStatus(OrdStatus.FILLED), 
                        order.Symbol,
                        order.Side, 
                        new LeavesQty(0),
                        new CumQty(order.OrderQty.getValue()),
                        new AvgPx(order.Price.getValue())
                    );

                    try
                    {
                        _session?.Send(executionReport);
                    }
                    catch (SessionNotFound ex)
                    {
                        Console.WriteLine("Erro ao enviar ExecutionReport: " + ex.Message);
                    }
                }
                else
                {
                    OrderCancelReject cancelReject = new(
                        new OrderID(GenOrderID()),
                        new ClOrdID(GenExecID()), 
                        new OrigClOrdID(GenOrigClOrdID()),
                        new OrdStatus(OrdStatus.REJECTED),
                        new CxlRejResponseTo(CxlRejResponseTo.ORDER_CANCEL_REQUEST)
                    );

                    try
                    {
                        _session?.Send(cancelReject);
                    }
                    catch (SessionNotFound ex)
                    {
                        Console.WriteLine("Erro ao enviar OrderCancelReject: " + ex.Message);
                    }
                }
            }
            else if (order.Side.getValue() == Side.SELL)
            {
                exposicaoAtual -= orderValue;
                ExecutionReport executionReport = new ExecutionReport(
                    new OrderID(GenOrderID()),
                    new ExecID(GenExecID()),
                    new ExecType(ExecType.FILL),
                    new OrdStatus(OrdStatus.FILLED),
                    order.Symbol, 
                    order.Side,
                    new LeavesQty(0), 
                    new CumQty(order.OrderQty.getValue()),
                    new AvgPx(order.Price.getValue()) 
                );

                try
                {
                    _session?.Send(executionReport);
                }
                catch (SessionNotFound ex)
                {
                    Console.WriteLine("Erro ao enviar ExecutionReport: " + ex.Message);
                }
            }
        }
    }
}
