using System;
using System.Collections.Generic;
using PCSC;
using PCSC.Iso7816;

namespace NfcReadAndSendToQueue
{
    public class Program
    {
        private static readonly IContextFactory _contextFactory = ContextFactory.Instance;

        public static void Main()
        {
            Console.WriteLine("This program will monitor all SmartCard readers and display all status changes.");

            // Retrieve the names of all installed readers.
            var readerNames = GetReaderNames();

            if (NoReaderFound(readerNames))
            {
                Console.WriteLine("There are currently no readers installed.");
                Console.ReadKey();
                return;
            }

            // Create smartcard monitor using a context factory. 
            // The context will be automatically released after monitor.Dispose()
            using (var monitor = new SCardMonitor(_contextFactory, SCardScope.System))
            {
                AttachToAllEvents(monitor); // Remember to detach if you use this in production!

                ShowUserInfo(readerNames);

                monitor.Start(readerNames);

                // Let the program run until the user presses CTRL-Q
                while (true)
                {
                    var key = Console.ReadKey();
                    if (ExitRequested(key))
                    {
                        break;
                    }
                    if (monitor.Monitoring)
                    {
                        monitor.Cancel();
                        Console.WriteLine("Monitoring paused. (Press CTRL-Q to quit)");
                    }
                    else
                    {
                        monitor.Start(readerNames);
                        Console.WriteLine("Monitoring started. (Press CTRL-Q to quit)");
                    }
                }
            }
        }

        private static bool ExitRequested(ConsoleKeyInfo key)
        {
            return key.Modifiers == ConsoleModifiers.Control
                   && key.Key == ConsoleKey.Q;
        }

        private static void ShowUserInfo(IEnumerable<string> readerNames)
        {
            foreach (var reader in readerNames)
            {
                Console.WriteLine($"Start monitoring for reader {reader}.");
            }
            Console.WriteLine("Press Ctrl-Q to exit or any key to toggle monitor.");
        }

        private static void AttachToAllEvents(ISCardMonitor monitor)
        {
            monitor.CardInserted += (sender, args) => CardInserted(monitor, args);
            monitor.CardRemoved += (sender, args) => CardRemoved(monitor, args);
            monitor.MonitorException += MonitorException;
        }

        private static void CardInserted(ISCardMonitor monitor, CardStatusEventArgs args)
        {
            string atr = BitConverter.ToString(args.Atr ?? new byte[0]);
            Console.WriteLine("Card inserted: {0}", atr);
            string uid;

            using (var context = _contextFactory.Establish(SCardScope.System))
            {
                var readerName = monitor.GetReaderName(0);
                if (readerName == null)
                {
                    return;
                }

                using (var nfcReader = new SCardReader(context))
                {
                    var transaction = nfcReader.Connect(readerName, SCardShareMode.Shared, SCardProtocol.Any);
                    if (transaction != SCardError.Success)
                    {
                        Console.WriteLine("Could not connect to reader {0}:\n{1}",
                            readerName,
                            SCardHelper.StringifyError(transaction));
                        return;
                    }

                    var apdu = new CommandApdu(IsoCase.Case2Short, nfcReader.ActiveProtocol)
                    {
                        CLA = 0xFF,
                        Instruction = InstructionCode.GetData,
                        P1 = 0x00,
                        P2 = 0x00,
                        Le = 0 // We don't know the ID tag size
                    };

                    transaction = nfcReader.BeginTransaction();
                    if (transaction != SCardError.Success)
                    {
                        Console.WriteLine("Could not begin transaction.");
                        return;
                    }

                    var receivePci = new SCardPCI(); // IO returned protocol control information.
                    var sendPci = SCardPCI.GetPci(nfcReader.ActiveProtocol);

                    var receiveBuffer = new byte[256];
                    var command = apdu.ToArray();

                    transaction = nfcReader.Transmit(
                        sendPci, // Protocol Control Information (T0, T1 or Raw)
                        command, // command APDU
                        receivePci, // returning Protocol Control Information
                        ref receiveBuffer); // data buffer

                    if (transaction == SCardError.Success)
                    {
                        var responseApdu = new ResponseApdu(receiveBuffer, IsoCase.Case2Short, nfcReader.ActiveProtocol);
                        uid = responseApdu.HasData ? BitConverter.ToString(responseApdu.GetData()) : "";

                        nfcReader.EndTransaction(SCardReaderDisposition.Leave);
                        nfcReader.Disconnect(SCardReaderDisposition.Reset);
                    }
                    else
                    {
                        Console.WriteLine("Error: " + SCardHelper.StringifyError(transaction));
                        nfcReader.EndTransaction(SCardReaderDisposition.Leave);
                        nfcReader.Disconnect(SCardReaderDisposition.Reset);
                        return;
                    }
                }
            }

            if (uid == "")
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Error: UID for ATR {0} is not found", atr);
                Console.ResetColor();
            }
            else
            {
                Console.WriteLine("Attempt to send auth action for UID: {0}", uid);
                uid = uid.Replace("-", "");
                ServiceBusSender.SendMessagesAsync(uid).Wait();
            }
        }

        private static void CardRemoved(ISCardMonitor monitor, CardStatusEventArgs unknown)
        {
            Console.WriteLine("Card removed");
        }

        private static void MonitorException(object sender, PCSCException ex)
        {
            Console.WriteLine("Monitor exited due an error:");
            Console.WriteLine(SCardHelper.StringifyError(ex.SCardError));
        }

        private static string[] GetReaderNames()
        {
            using (var context = _contextFactory.Establish(SCardScope.System))
            {
                return context.GetReaders();
            }
        }

        private static bool NoReaderFound(ICollection<string> readerNames)
        {
            return readerNames == null || readerNames.Count < 1;
        }
    }
}