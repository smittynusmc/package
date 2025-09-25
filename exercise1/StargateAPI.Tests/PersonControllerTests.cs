// Tests/PersonControllerTests.cs
using System.Net;
using FluentAssertions;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using StargateAPI.Controllers;
using StargateAPI.Business.Commands;

namespace StargateAPI.Tests;

public class PersonControllerTests
{
    private sealed class FakeMediator : IMediator
    {
        private readonly Func<object, object?> _onSend;
        public FakeMediator(Func<object, object?> onSend) => _onSend = onSend;

        // For IRequest<TResponse>
        public Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
            => Task.FromResult((TResponse)_onSend(request)!);

        // For IRequest (no response)
        public Task Send<TRequest>(TRequest request, CancellationToken cancellationToken = default)
            where TRequest : IRequest
            => Task.CompletedTask;

        // Streams (unused in your tests)
        public IAsyncEnumerable<TResponse> CreateStream<TResponse>(IStreamRequest<TResponse> request, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();
        public IAsyncEnumerable<object?> CreateStream(object request, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        // Object-based Send (rarely used)
        public Task<object?> Send(object request, CancellationToken cancellationToken = default)
            => Task.FromResult(_onSend(request));

        // Publish (no-ops for tests)
        public Task Publish(object notification, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default) where TNotification : INotification => Task.CompletedTask;
    }


    [Fact]
    public async Task CreatePerson_MapsSuccessStatusCode()
    {
        var mediator = new FakeMediator(req =>
        {
            if (req is CreatePerson) return new CreatePersonResult { Success = true, ResponseCode = 201, Id = 42, Message = "Created." };
            throw new InvalidOperationException();
        });

        var ctrl = new PersonController(mediator);
        var result = await ctrl.CreatePerson("Teal'c");
        var obj = result as ObjectResult;

        obj.Should().NotBeNull();
        obj!.StatusCode.Should().Be(201); // set by GetResponse
        (obj.Value as BaseResponse)!.Success.Should().BeTrue();
    }

    [Fact]
    public async Task CreatePerson_MapsHttpRequestExceptionTo409()
    {
        var mediator = new FakeMediator(_ => throw new HttpRequestException("That person name already exists.", null, HttpStatusCode.Conflict));
        var ctrl = new PersonController(mediator);

        var result = await ctrl.CreatePerson("Jack O'Neill");
        var obj = result as ObjectResult;

        obj!.StatusCode.Should().Be(409);
        (obj.Value as BaseResponse)!.Success.Should().BeFalse();
    }
}
