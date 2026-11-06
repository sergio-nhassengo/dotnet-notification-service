using System.Text.Json;
using Application.Common.Persistence;
using Application.Notifications.Contracts;
using Domain.Common;
using Domain.Entities;
using Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Notifications.Commands.CreateEmail;

public sealed record EmailAcceptedResponse(Guid NotificationId, Guid MessageId, string Status);
