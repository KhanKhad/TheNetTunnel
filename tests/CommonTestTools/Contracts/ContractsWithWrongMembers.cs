using System;
using TheNetTunnel.Contract;

namespace CommonTestTools.Contracts;

public interface IContractWithMethodWithoutAttribute
{
    void MethodWithoutAttribute();
}

public interface IContractWithDelegateWithoutAttribute
{
    Action ActionWithoutAttribute { get; set; }
}
    
public interface IContractWithEventWithoutAttribute
{
    event Action ActionWithoutAttribute;
}

public interface IContractWithPropertyWithoutAttribute
{
    int propertyWithoutAttribute { get; set; }
}

public interface IContractWithNonDelegateProperty
{
    [TntMessageAttribute(1)]
    int propertyWithoutAttribute { get; set; }
}
public interface IContractWithSameAskAndEventId
{
    [TntMessageAttribute(1)]
    string Ask();

    [TntMessageAttribute(1)]
    Func<int> OnAsk { get; set; }
}
public interface IContractWithSameEventId
{
    [TntMessageAttribute(1)]
    Action OnSay { get; set; }

    [TntMessageAttribute(1)]
    Func<int> OnAsk { get; set; }
}
public interface IContractWithSameSayId
{
    [TntMessageAttribute(1)]
    void Say1();

    [TntMessageAttribute(1)]
    void Say2();
}
public interface IUnserializeableContract
{
    [TntMessageAttribute(1)]
    void Say(EventArgs arg);
}

public interface IUnDeserializeableContract
{
    [TntMessageAttribute(1)]
    EventArgs Ask();
}