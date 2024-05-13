using QuickFix;
using QuickFix.Fields;
using QuickFix.FIX44;
using Message = QuickFix.Message;

namespace OrderGeneratorMicroservice
{
    class Program
    {
        static void Main(string[] args)
        {
            try
            {
                SessionSettings settings = new("ordergenerator.cfg");
                IMessageStoreFactory storeFactory = new FileStoreFactory(settings);
                ILogFactory logFactory = new FileLogFactory(settings);
                IMessageFactory messageFactory = new DefaultMessageFactory();

                OrderGeneratorApplication application = new();

                QuickFix.Transport.SocketInitiator initiator = new(application, storeFactory, settings, logFactory, messageFactory);
                initiator.Start();

                Console.WriteLine("Pressione qualquer tecla para encerrar.");
                Console.ReadKey();

                initiator.Stop();
            }
            catch (Exception ex)
            {
                Console.WriteLine("Ocorreu uma exceção: " + ex.Message);
            }
        }
    }

    class OrderGeneratorApplication : MessageCracker, IApplication
    {
        Session? _session;
        Random _random = new();
        string[] _symbols = { "PETR4", "VALE3", "VIIA4" };
        char[] _sides = { '1', '2' }; // '1' for Buy, '2' for Sell
        int clOrdID = 0;

        private string GenClOrdID() { return (++clOrdID).ToString(); }

        public void FromApp(Message message, SessionID sessionId) { }
        public void OnCreate(SessionID sessionId) { }
        public void OnLogon(SessionID sessionId)
        {
            _session = Session.LookupSession(sessionId);

            while (true)
            {
                NewOrderSingle newOrder = GenerateNewOrderSingle();
                SendOrder(newOrder);
            }
        }
        public void OnLogout(SessionID sessionId) { }
        public void ToAdmin(Message message, SessionID sessionId) { }
        public void ToApp(Message message, SessionID sessionId) { }
        public void FromAdmin(Message message, SessionID sessionId) { }

        public static void OnMessage(ExecutionReport report, SessionID sessionID)
        {
            Console.WriteLine("ExecutionReport recebida: " + report.ToString());
        }

        public void SendOrder(NewOrderSingle order)
        {
            _session?.Send(order);
        }

        private NewOrderSingle GenerateNewOrderSingle()
        {
            string symbol = _symbols[_random.Next(0, _symbols.Length)];
            char side = _sides[_random.Next(0, _sides.Length)];

            NewOrderSingle newOrder = new(
                new ClOrdID(GenClOrdID()), 
                new Symbol(symbol), 
                new Side(side), 
                new TransactTime(DateTime.Now), 
                new OrdType(OrdType.LIMIT) 
            );

            newOrder.Set(new OrderQty(_random.Next(1, 100000)));
            newOrder.Set(new Price((decimal)(_random.Next(1, 100000) / 100.0))); 

            return newOrder;
        }
    }
}
