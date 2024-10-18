using CommonTestTools.Contracts;
using CommonTestTools;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Tnt.LongTests.ContractMocks;
using System.Collections.Concurrent;
using TNT.Core.Presentation;

namespace TNT.Integration.LongTests
{
    [TestFixture]
    public class ConcurrentTests
    {
        private ServerAndClient<ILongTestContract<Company>, ILongTestContract<Company>, LongTestContract<Company>> _serverAndClientCompany;
        private ServerAndClient<ILongTestContract<string>, ILongTestContract<string>, LongTestContract<string>> _serverAndClientString;

        private LongTestContract<Company> _serverContractCompany;
        private LongTestContract<string> _serverContractString;

        [SetUp]
        public async Task SetUp()
        {
            _serverAndClientCompany = await ServerAndClient<ILongTestContract<Company>, ILongTestContract<Company>, LongTestContract<Company>>.Create();

            _serverContractCompany = (LongTestContract<Company>)_serverAndClientCompany.ServerSideConnection.Contract;

            _serverAndClientString = await ServerAndClient<ILongTestContract<string>, ILongTestContract<string>, LongTestContract<string>>.Create(12346);

            _serverContractString = (LongTestContract<string>)_serverAndClientString.ServerSideConnection.Contract;
        }

        [TearDown]
        public void Disposing()
        {
            _serverAndClientCompany.Dispose();
            _serverAndClientString.Dispose();
        }

        [TestCase(2)]
        [TestCase(10)]
        [TestCase(100)]
        [TestCase(500)]
        [TestCase(800)]
        [TestCase(900)]
        [TestCase(990)]
        [TestCase(1024)]
        [TestCase(2 * 1024)]
        [TestCase(ushort.MaxValue + 1)]
        [TestCase(ushort.MaxValue * 2 - 1)]
        [TestCase(ushort.MaxValue * 2)]
        public async Task StringPackets_transmitsViaTcp_Concurrent(int stringLengthInBytes)
        {
            //Tasks count:
            int sentCount = 10;

            string originStringArgument = generateRandomString(stringLengthInBytes / 2);

            var sentTasks = new List<Task>(sentCount * 4);

            for (var i = 0; i < sentCount; i++)
            {
                sentTasks.Add(Task.Run(() => _serverAndClientString.ClientSideConnection.Contract.Ask(originStringArgument)));
                sentTasks.Add(Task.Run(() => _serverAndClientString.ClientSideConnection.Contract.Say(originStringArgument)));
                sentTasks.Add(Task.Run(async () => await _serverAndClientString.ClientSideConnection.Contract.SayAsync(originStringArgument)));
                sentTasks.Add(Task.Run(async () => await _serverAndClientString.ClientSideConnection.Contract.AskAsync(originStringArgument)));
            }

            await Task.WhenAll(sentTasks);

            await Task.Delay(1000);

            var arr = _serverContractString.Messages.ToArray();

            Assert.That(sentCount * 4 == arr.Length);
            foreach (var received in arr)
            {
                Assert.That(originStringArgument == received);
            }

        }

        [TestCase(2)]
        [TestCase(10)]
        [TestCase(100)]
        [TestCase(500)]
        [TestCase(800)]
        [TestCase(900)]
        [TestCase(990)]
        [TestCase(1024)]
        [TestCase(2 * 1024)]
        [TestCase(ushort.MaxValue + 1)]
        [TestCase(ushort.MaxValue * 2 - 1)]
        [TestCase(ushort.MaxValue * 2)]
        public async Task StringPackets_transmitsViaTcp_Concurrent2(int stringLengthInBytes)
        {
            //Tasks count:
            int sentCount = 10;

            string originStringArgument = generateRandomString(stringLengthInBytes / 2);

            var sentTasks = new List<Task>(sentCount * 4);
            var bag = new ConcurrentBag<string>();

            _serverAndClientString.ClientSideConnection.Contract.OnSay += (a) =>
            {
                bag.Add(a);
            };
            _serverAndClientString.ClientSideConnection.Contract.OnAsk += (a) =>
            {
                bag.Add(a);
                return true;
            };

            _serverAndClientString.ClientSideConnection.Contract.OnSayAsync += async (a) =>
            {
                bag.Add(a);
                await Task.Yield();
            };
            _serverAndClientString.ClientSideConnection.Contract.OnAskAsync += async (a) =>
            {
                bag.Add(a);
                await Task.Yield();
                return true;
            };

            for (var i = 0; i < sentCount; i++)
            {
                sentTasks.Add(Task.Run(() => _serverAndClientString.ServerSideConnection.Contract.OnAsk(originStringArgument)));
                sentTasks.Add(Task.Run(() => _serverAndClientString.ServerSideConnection.Contract.OnSay(originStringArgument)));
                sentTasks.Add(Task.Run(async () => await _serverAndClientString.ServerSideConnection.Contract.OnSayAsync(originStringArgument)));
                sentTasks.Add(Task.Run(async () => await _serverAndClientString.ServerSideConnection.Contract.OnAskAsync(originStringArgument)));
            }

            await Task.WhenAll(sentTasks);

            await Task.Delay(1000);

            var arr = bag.ToArray();

            Assert.That(sentCount * 4 == arr.Length);
            foreach (var received in arr)
            {
                Assert.That(originStringArgument == received);
            }

        }


        [TestCase(2)]
        [TestCase(10)]
        [TestCase(100)]
        [TestCase(500)]
        [TestCase(800)]
        [TestCase(900)]
        [TestCase(990)]
        [TestCase(1024)]
        [TestCase(2 * 1024)]
        [TestCase(ushort.MaxValue + 1)]
        [TestCase(ushort.MaxValue * 2 - 1)]
        [TestCase(ushort.MaxValue * 2)]
        public async Task StringPackets_transmitsViaTcp_Concurrent3(int stringLengthInBytes)
        {
            //Tasks count:
            int sentCount = 10;

            string originStringArgument = generateRandomString(stringLengthInBytes / 2);

            var sentTasks = new List<Task>(sentCount * 8);
            var bag = new ConcurrentBag<string>();

            _serverAndClientString.ClientSideConnection.Contract.OnSay += (a) =>
            {
                bag.Add(a);
            };
            _serverAndClientString.ClientSideConnection.Contract.OnAsk += (a) =>
            {
                bag.Add(a);
                return true;
            };

            _serverAndClientString.ClientSideConnection.Contract.OnSayAsync += async (a) =>
            {
                bag.Add(a);
                await Task.Yield();
            };
            _serverAndClientString.ClientSideConnection.Contract.OnAskAsync += async (a) =>
            {
                bag.Add(a);
                await Task.Yield();
                return true;
            };

            for (var i = 0; i < sentCount; i++)
            {
                sentTasks.Add(Task.Run(() => _serverAndClientString.ClientSideConnection.Contract.Ask(originStringArgument)));
                sentTasks.Add(Task.Run(() => _serverAndClientString.ClientSideConnection.Contract.Say(originStringArgument)));
                sentTasks.Add(Task.Run(async () => await _serverAndClientString.ClientSideConnection.Contract.SayAsync(originStringArgument)));
                sentTasks.Add(Task.Run(async () => await _serverAndClientString.ClientSideConnection.Contract.AskAsync(originStringArgument)));
                sentTasks.Add(Task.Run(() => _serverAndClientString.ServerSideConnection.Contract.OnAsk(originStringArgument)));
                sentTasks.Add(Task.Run(() => _serverAndClientString.ServerSideConnection.Contract.OnSay(originStringArgument)));
                sentTasks.Add(Task.Run(async () => await _serverAndClientString.ServerSideConnection.Contract.OnSayAsync(originStringArgument)));
                sentTasks.Add(Task.Run(async () => await _serverAndClientString.ServerSideConnection.Contract.OnAskAsync(originStringArgument)));
            }

            await Task.WhenAll(sentTasks);

            await Task.Delay(1000);

            var arr1 = bag.ToArray();
            var arr2 = _serverContractString.Messages.ToArray();

            Assert.That(sentCount * 4 == arr1.Length);
            Assert.That(sentCount * 4 == arr2.Length);

            foreach (var received in arr1)
            {
                Assert.That(originStringArgument == received);
            }
            foreach (var received in arr2)
            {
                Assert.That(originStringArgument == received);
            }
        }



        [TestCase(1, 40)]
        [TestCase(10, 40)]
        [TestCase(20, 40)]
        [TestCase(50, 40)]
        [TestCase(80, 40)]
        [TestCase(100, 40)]
        [TestCase(200, 40)]
        [TestCase(400, 40)]
        [TestCase(1000, 40)]
        public async Task ProtobuffPackets_transmitViaTcp_Concurrent(int sizeOfCompanyInUsers, int parralelTasksCount)
        {
            //1000 = 0.5mb  
            //2000 = 2mb    
            //5000 = 10mb   
            //10000 = 50mb  5

            var originCompany = IntegrationTestsHelper.CreateCompany(sizeOfCompanyInUsers);


            var sentTasks = new List<Task>(parralelTasksCount * 4);

            for (var i = 0; i < parralelTasksCount; i++)
            {
                sentTasks.Add(Task.Run(() => _serverAndClientCompany.ClientSideConnection.Contract.Ask(originCompany)));
                sentTasks.Add(Task.Run(() => _serverAndClientCompany.ClientSideConnection.Contract.Say(originCompany)));
                sentTasks.Add(Task.Run(async () => await _serverAndClientCompany.ClientSideConnection.Contract.SayAsync(originCompany)));
                sentTasks.Add(Task.Run(async () => await _serverAndClientCompany.ClientSideConnection.Contract.AskAsync(originCompany)));
            }

            await Task.WhenAll(sentTasks);

            await Task.Delay(1000);

            var arr = _serverContractCompany.Messages.ToArray();

            Assert.That(parralelTasksCount * 4 == arr.Length);
            foreach (var received in arr)
            {
                originCompany.AssertIsSameTo(received);
            }
        }

        [TestCase(1, 40)]
        [TestCase(10, 40)]
        [TestCase(20, 40)]
        [TestCase(50, 40)]
        [TestCase(80, 40)]
        [TestCase(100, 40)]
        [TestCase(200, 40)]
        [TestCase(400, 40)]
        [TestCase(1000, 40)]
        public async Task ProtobuffPackets_transmitViaTcp_Concurrent2(int sizeOfCompanyInUsers, int parralelTasksCount)
        {
            var originCompany = IntegrationTestsHelper.CreateCompany(sizeOfCompanyInUsers);

            var sentTasks = new List<Task>(parralelTasksCount);

            var bag = new ConcurrentBag<Company>();

            _serverAndClientCompany.ClientSideConnection.Contract.OnSay += (a) =>
            {
                bag.Add(a);
            };
            _serverAndClientCompany.ClientSideConnection.Contract.OnAsk += (a) =>
            {
                bag.Add(a);
                return true;
            };

            _serverAndClientCompany.ClientSideConnection.Contract.OnSayAsync += async (a) =>
            {
                bag.Add(a);
                await Task.Yield();
            };
            _serverAndClientCompany.ClientSideConnection.Contract.OnAskAsync += async (a) =>
            {
                bag.Add(a);
                await Task.Yield();
                return true;
            };

            for (var i = 0; i < parralelTasksCount; i++)
            {
                sentTasks.Add(Task.Run(() => _serverAndClientCompany.ServerSideConnection.Contract.OnAsk(originCompany)));
                sentTasks.Add(Task.Run(() => _serverAndClientCompany.ServerSideConnection.Contract.OnSay(originCompany)));
                sentTasks.Add(Task.Run(async () => await _serverAndClientCompany.ServerSideConnection.Contract.OnSayAsync(originCompany)));
                sentTasks.Add(Task.Run(async () => await _serverAndClientCompany.ServerSideConnection.Contract.OnAskAsync(originCompany)));
            }

            await Task.WhenAll(sentTasks);

            await Task.Delay(1000);

            var arr = bag.ToArray();

            Assert.That(parralelTasksCount * 4 == arr.Length);

            foreach (var received in arr)
            {
                originCompany.AssertIsSameTo(received);
            }
        }

        [TestCase(1, 40)]
        [TestCase(10, 40)]
        [TestCase(20, 40)]
        [TestCase(50, 40)]
        [TestCase(80, 40)]
        [TestCase(100, 40)]
        [TestCase(200, 40)]
        [TestCase(400, 40)]
        [TestCase(1000, 40)]
        public async Task ProtobuffPackets_transmitViaTcp_Concurrent3(int sizeOfCompanyInUsers, int parralelTasksCount)
        {
            var originCompany = IntegrationTestsHelper.CreateCompany(sizeOfCompanyInUsers);

            var sentTasks = new List<Task>(parralelTasksCount * 8);

            var bag = new ConcurrentBag<Company>();

            _serverAndClientCompany.ClientSideConnection.Contract.OnSay += (a) =>
            {
                bag.Add(a);
            };
            _serverAndClientCompany.ClientSideConnection.Contract.OnAsk += (a) =>
            {
                bag.Add(a);
                return true;
            };

            _serverAndClientCompany.ClientSideConnection.Contract.OnSayAsync += async (a) =>
            {
                bag.Add(a);
                await Task.Yield();
            };
            _serverAndClientCompany.ClientSideConnection.Contract.OnAskAsync += async (a) =>
            {
                bag.Add(a);
                await Task.Yield();
                return true;
            };

            for (var i = 0; i < parralelTasksCount; i++)
            {
                sentTasks.Add(Task.Run(() => _serverAndClientCompany.ClientSideConnection.Contract.Ask(originCompany)));
                sentTasks.Add(Task.Run(() => _serverAndClientCompany.ClientSideConnection.Contract.Say(originCompany)));
                sentTasks.Add(Task.Run(async () => await _serverAndClientCompany.ClientSideConnection.Contract.SayAsync(originCompany)));
                sentTasks.Add(Task.Run(async () => await _serverAndClientCompany.ClientSideConnection.Contract.AskAsync(originCompany)));

                sentTasks.Add(Task.Run(() => _serverAndClientCompany.ServerSideConnection.Contract.OnAsk(originCompany)));
                sentTasks.Add(Task.Run(() => _serverAndClientCompany.ServerSideConnection.Contract.OnSay(originCompany)));
                sentTasks.Add(Task.Run(async () => await _serverAndClientCompany.ServerSideConnection.Contract.OnSayAsync(originCompany)));
                sentTasks.Add(Task.Run(async () => await _serverAndClientCompany.ServerSideConnection.Contract.OnAskAsync(originCompany)));
            }

            await Task.WhenAll(sentTasks);

            await Task.Delay(1000);

            var arr1 = bag.ToArray();
            var arr2 = _serverContractCompany.Messages.ToArray();

            Assert.That(parralelTasksCount * 4 == arr1.Length);
            Assert.That(parralelTasksCount * 4 == arr2.Length);

            foreach (var received in arr1)
            {
                originCompany.AssertIsSameTo(received);
            }
            foreach (var received in arr2)
            {
                originCompany.AssertIsSameTo(received);
            }
        }

        public string generateRandomString(int length)
        {
            var random = new Random(DateTime.Now.Millisecond);
            //Initiate objects & vars    Random random = new Random();
            var randomString = "";
            int randNumber;

            //Loop ‘length’ times to generate a random number or character
            for (int i = 0; i < length; i++)
            {
                if (random.Next(1, 3) == 1)
                    randNumber = random.Next(97, 123); //char {a-z}
                else
                    randNumber = random.Next(48, 58); //int {0-9}

                //append random char or digit to random string
                randomString = randomString + (char)randNumber;
            }

            //return the random string
            return randomString;
        }
    }
}
