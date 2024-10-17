using CommonTestTools;
using CommonTestTools.Contracts;
using NUnit.Framework;
using System.Threading.Tasks;
using TNT.Core.Api;
using TNT.Core.Exceptions.ContractImplementation;

namespace TNT.Core.Tests.ContractBuilderTests
{
    [TestFixture]
    public class ContractBuildExceptionsTests
    {
        [Test]
        public async Task EmptyContract_Creates()
        {
            using var client = await TntBuilder.UseContract<IEmptyContract>()
                .UseChannel(new ChannelMock())
                .BuildAsync();

            Assert.That(client, Is.Not.Null);
        }
        [Test]
        public void SayCordIdDuplicated_CreateT_throwsException()
        {
            var client = TntBuilder.UseContract<IContractWithSameSayId>()
                .UseChannel(new ChannelMock());

            Assert.Throws<ContractMessageIdDuplicateException>(() => client.Build());
        }

        [Test]
        public void EventCordIdDuplicated_CreateT_throwsException()
        {
            var client = TntBuilder.UseContract<IContractWithSameEventId>()
                .UseChannel(new ChannelMock());

            Assert.Throws<ContractMessageIdDuplicateException>(() => client.Build());
        }

        [Test]
        public void AskAndEventCordIdDuplicated_CreateT_throwsException()
        {
            var client = TntBuilder.UseContract<IContractWithSameAskAndEventId>()
                .UseChannel(new ChannelMock());

            Assert.Throws<ContractMessageIdDuplicateException>(() => client.Build());
        }

        [Test]
        public void PropertyDoesNotContainAttribute_CreateT_throwsException()
        {
            var client = TntBuilder.UseContract<IContractWithPropertyWithoutAttribute>()
                .UseChannel(new ChannelMock());

            Assert.Throws<ContractMemberAttributeMissingException>(() => client.Build());
        }

        [Test]
        public void MethodDoesNotContainAttribute_CreateT_throwsException()
        {
            var client = TntBuilder.UseContract<IContractWithMethodWithoutAttribute>()
                .UseChannel(new ChannelMock());

            Assert.Throws<ContractMemberAttributeMissingException>(() => client.Build());
        }

        [Test]
        public void DelegateDoesNotContainAttribute_CreateT_throwsException()
        {
            var client = TntBuilder.UseContract<IContractWithDelegateWithoutAttribute>()
                .UseChannel(new ChannelMock());

            Assert.Throws<ContractMemberAttributeMissingException>(() => client.Build());
        }
        [Test]
        public void ContractWithNonDelegateProperty_CreateT_throwsException()
        {
            var client = TntBuilder.UseContract<IContractWithNonDelegateProperty>()
                .UseChannel(new ChannelMock());

            Assert.Throws<InvalidContractMemeberException>(() => client.Build());
        }
    }
}
