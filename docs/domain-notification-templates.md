# Domain notification templates

28 domain-specific templates are shipped without template versions, in addition to the existing `payment-confirmed` template. All changes are confined to the notification service.

The templates are based on notification workflows in OTC, Weighbridge and TAN. They are English emails with matching HTML and plain-text bodies. They do not query client databases, generate reports, choose recipients, or trigger themselves.

## Request contract

Use `POST /api/v1/notifications/email` with application authentication. Set `templateId` and `variables`; omit `htmlBody`, `textBody`, and normally `subject`. Existing application profiles allow only their listed templates. The legacy profile remains unchanged.

All variables listed below are required nonempty strings. Supply plain text, not HTML. Use `Not applicable` or `Not provided` for unavailable contextual details; do not invent transaction or payment identifiers. Format dates with an explicit timezone and amounts/masses with the indicated units before submission. Variable values are HTML-encoded and never evaluated as further template placeholders.

Templates marked **attachment required** need at least one ordinary attachment reference, in addition to their variables. An inline logo does not satisfy this requirement. Upload the generated document through `/api/v1/notifications/attachments` first.

`metadata.json` alongside each template contains its variable descriptions, example values, application ownership and source-workflow reference. Examples are illustrative, not production data.

## Template catalogue

| Template ID | Applications | Attachment |
|---|---|---|
| `otc-additional-cargo-requested` | `otc-api` | Optional |
| `otc-additional-cargo-approved` | `otc-api` | Optional |
| `otc-additional-cargo-rejected` | `otc-api` | Optional |
| `otc-work-order-approval-requested` | `otc-api` | Optional |
| `otc-vessel-arrival-updated` | `otc-api` | Optional |
| `otc-vessel-grt-not-billable` | `otc-api` | Optional |
| `otc-bank-guarantees-expiring` | `otc-api` | Required |
| `vessel-bulletin-report` | `otc-api`, `weighbridge-api` | Required |
| `weighbridge-truck-on-hold` | `weighbridge-api` | Optional |
| `weighbridge-truck-release-approved` | `weighbridge-api` | Optional |
| `weighbridge-truck-release-rejected` | `weighbridge-api` | Optional |
| `weighbridge-shipping-activated` | `weighbridge-api` | Optional |
| `weighbridge-shipping-deactivated` | `weighbridge-api` | Optional |
| `rail-consignment-alert` | `weighbridge-api`, `tan-webservice` | Optional |
| `weighbridge-wagon-confirmed` | `weighbridge-api` | Optional |
| `weighbridge-wagon-declined` | `weighbridge-api` | Optional |
| `weighbridge-equipment-approval-requested` | `weighbridge-api` | Optional |
| `tan-tip-generated` | `tan-webservice` | Optional |
| `tan-tip-payment-submitted` | `tan-webservice` | Optional |
| `tan-tip-payment-confirmed` | `tan-webservice` | Optional |
| `tan-tip-payment-failed` | `tan-webservice` | Optional |
| `tan-truck-release-requested` | `tan-webservice` | Optional |
| `tan-cbr-vessel-change-requested` | `tan-webservice` | Optional |
| `tan-sew-integration-failed` | `tan-webservice` | Optional |
| `weighbridge-ticket` | `otc-api`, `weighbridge-api`, `tan-webservice` | Required |
| `weighbridge-transactions-report` | `otc-api`, `weighbridge-api`, `tan-webservice` | Required |
| `data-integrity-report` | `otc-api`, `weighbridge-api`, `tan-webservice` | Required |
| `tan-trips-history-report` | `tan-webservice` | Required |

## otc-additional-cargo-requested

An additional cargo requisition has been submitted and awaits approval.

Applications: `otc-api`.

| Variable | Meaning | Example |
|---|---|---|
| `requestId` | Request | AC-102 |
| `vesselName` | Vessel | MV Example |
| `workOrderId` | Work order | WO-1204 |
| `agentName` | Agent | Example Agent |

Source workflow: `otc-api/Application/Weighbridge/AdditionalCargo`.

## otc-additional-cargo-approved

The additional cargo request has been approved.

Applications: `otc-api`.

| Variable | Meaning | Example |
|---|---|---|
| `requestId` | Request | AC-102 |
| `vesselName` | Vessel | MV Example |
| `workOrderId` | Work order | WO-1204 |
| `agentName` | Agent | Example Agent |
| `quantityKg` | Quantity to add (kg) | 25,000 |

Source workflow: `otc-api/Application/Weighbridge/AdditionalCargo`.

## otc-additional-cargo-rejected

The additional cargo request has been rejected.

Applications: `otc-api`.

| Variable | Meaning | Example |
|---|---|---|
| `requestId` | Request | AC-102 |
| `vesselName` | Vessel | MV Example |
| `workOrderId` | Work order | WO-1204 |
| `agentName` | Agent | Example Agent |
| `reason` | Rejection reason | The requested quantity exceeds the approved allocation. |

Source workflow: `otc-api/Application/Weighbridge/AdditionalCargo`.

## otc-work-order-approval-requested

A work order requires your review.

Applications: `otc-api`.

| Variable | Meaning | Example |
|---|---|---|
| `vesselName` | Vessel | MV Example |
| `workOrderId` | Work order | WO-1204 |
| `requesterName` | Requested by | Example Requester |
| `approvalStage` | Approval stage | Operations |
| `submittedAt` | Submitted at | 09 Sep 2026 10:00 CAT |
| `summary` | Work requested | Review the proposed cargo operation. |

Source workflow: `otc-api/API/Controllers/WorkOrderController.cs`.

## otc-vessel-arrival-updated

Arrival or berthing information has been updated.

Applications: `otc-api`.

| Variable | Meaning | Example |
|---|---|---|
| `vesselName` | Vessel | MV Example |
| `workOrderId` | Work order | WO-1204 |
| `marineVoyageNumber` | Marine voyage / Contra-Marca | MV-204 |
| `arrivalAt` | Arrival | 09 Sep 2026 10:00 CAT |
| `berthingAt` | Berthing | 09 Sep 2026 12:00 CAT |
| `berth` | Berth | Berth 1 |

Source workflow: `otc-api/Application/VesselBerthings/UpdateArrivalAndBerthingDates.cs`.

## otc-vessel-grt-not-billable

The vessel GRT will not be considered for billing to the service provider listed below.

Applications: `otc-api`.

| Variable | Meaning | Example |
|---|---|---|
| `vesselName` | Vessel | MV Example |
| `workOrderId` | Work order | WO-1204 |
| `grt` | GRT | 35,000 |
| `marineVoyageNumber` | Marine voyage / Contra-Marca | MV-204 |
| `departureAt` | Departure | 09 Sep 2026 18:00 CAT |
| `serviceProvider` | Service provider | Example Marine Services |

Source workflow: `otc-api/Application/WorkOrders/MarkVesselDeparture.cs`.

## otc-bank-guarantees-expiring

Please find attached the report of bank guarantees approaching expiry.

Applications: `otc-api`. **Attachment required.**

| Variable | Meaning | Example |
|---|---|---|
| `expiryWindowDays` | Expiry window (days) | 30 |
| `reportDate` | Report date | 09 Sep 2026 |
| `summary` | Report summary | Review the attached guarantees and arrange renewals where required. |

Source workflow: `otc-api/API/Workers/NotifyBankGuarantees30DaysJob.cs`.

## vessel-bulletin-report

Please find attached the vessel bulletin for the shift below.

Applications: `otc-api`, `weighbridge-api`. **Attachment required.**

| Variable | Meaning | Example |
|---|---|---|
| `vesselName` | Vessel | MV Example |
| `workOrderId` | Work order | WO-1204 |
| `shiftNumber` | Shift | 2 |
| `shiftDate` | Shift date | 09 Sep 2026 |

Source workflow: `otc-api/Infrastructure/Services/SSRSReportService.cs; weighbridge-web-api/WeighBridgeWeAPI/Infrastructure/Services/SSRSReportService.cs`.

## weighbridge-truck-on-hold

A truck has been placed on hold and requires follow-up.

Applications: `weighbridge-api`.

| Variable | Meaning | Example |
|---|---|---|
| `registrationNumber` | Truck | AAA-123-MP |
| `tanId` | TAN | TAN-1204 |
| `orderNumber` | Order | ORD-204 |
| `trailer1` | Trailer 1 | TRL-101 |
| `trailer2` | Trailer 2 | Not applicable |
| `customerName` | Customer | Example Cargo |
| `commodity` | Commodity | Coal |
| `destination` | Destination / slab | Terminal 1 |
| `mineTicketNumber` | Mine ticket | MT-204 |
| `mineGrossMass` | Mine gross mass (kg) | 48,000 |
| `grossMass` | First mass (kg) | 47,800 |
| `parcelNumber` | Parcel | PAR-102 |
| `transporterName` | Transporter | Example Transporter |
| `onHoldAt` | On hold since | 09 Sep 2026 10:00 CAT |
| `tareMass` | Tare mass (kg) | 18,000 |
| `netMass` | Net mass (kg) | 29,800 |
| `reason` | Reason | Weight discrepancy requires review. |

Source workflow: `weighbridge-web-api/WeighBridgeWeAPI/Application/ReceiptTransactions/Commands/PutOnHoldTransaction.cs`.

## weighbridge-truck-release-approved

Operations has approved the truck release request.

Applications: `weighbridge-api`.

| Variable | Meaning | Example |
|---|---|---|
| `registrationNumber` | Truck | AAA-123-MP |
| `tanId` | TAN | TAN-1204 |
| `orderNumber` | Order | ORD-204 |
| `trailer1` | Trailer 1 | TRL-101 |
| `trailer2` | Trailer 2 | Not applicable |
| `customerName` | Customer | Example Cargo |
| `commodity` | Commodity | Coal |
| `destination` | Destination / slab | Terminal 1 |
| `mineTicketNumber` | Mine ticket | MT-204 |
| `mineGrossMass` | Mine gross mass (kg) | 48,000 |
| `grossMass` | First mass (kg) | 47,800 |
| `onHoldAt` | On hold since | 09 Sep 2026 10:00 CAT |
| `reason` | Original hold reason | Weight discrepancy requires review. |
| `agentComment` | Agent comment | Supporting documents have been provided. |
| `supervisorComment` | Operations supervisor comment | Review completed; see the recorded decision. |

Source workflow: `weighbridge-web-api/WeighBridgeWeAPI/Application/ReceiptTransactions/Commands/ApproveReleasedTransaction.cs`.

## weighbridge-truck-release-rejected

Operations has rejected the truck release request. The truck cannot continue in the process.

Applications: `weighbridge-api`.

| Variable | Meaning | Example |
|---|---|---|
| `registrationNumber` | Truck | AAA-123-MP |
| `tanId` | TAN | TAN-1204 |
| `orderNumber` | Order | ORD-204 |
| `trailer1` | Trailer 1 | TRL-101 |
| `trailer2` | Trailer 2 | Not applicable |
| `customerName` | Customer | Example Cargo |
| `commodity` | Commodity | Coal |
| `destination` | Destination / slab | Terminal 1 |
| `mineTicketNumber` | Mine ticket | MT-204 |
| `mineGrossMass` | Mine gross mass (kg) | 48,000 |
| `grossMass` | First mass (kg) | 47,800 |
| `onHoldAt` | On hold since | 09 Sep 2026 10:00 CAT |
| `reason` | Original hold reason | Weight discrepancy requires review. |
| `agentComment` | Agent comment | Supporting documents have been provided. |
| `supervisorComment` | Operations supervisor comment | Review completed; see the recorded decision. |

Source workflow: `weighbridge-web-api/WeighBridgeWeAPI/Application/ReceiptTransactions/Commands/RejectReleasedTransaction.cs`.

## weighbridge-shipping-activated

The cargo below has been activated for the shipping sequence.

Applications: `weighbridge-api`.

| Variable | Meaning | Example |
|---|---|---|
| `shippingOrderId` | Shipping order | SO-104 |
| `commodity` | Commodity | Coal |
| `customerName` | Customer | Example Cargo |
| `parcelNumber` | Parcel | PAR-102 |
| `hatch` | Hatch | Hatch 2 |
| `position` | Position | 1 |

Source workflow: `weighbridge-web-api/WeighBridgeWeAPI/Application/ShippingOrders/ActivateShippingOrderDetail.cs`.

## weighbridge-shipping-deactivated

The cargo below has been deactivated for the shipping sequence.

Applications: `weighbridge-api`.

| Variable | Meaning | Example |
|---|---|---|
| `shippingOrderId` | Shipping order | SO-104 |
| `commodity` | Commodity | Coal |
| `customerName` | Customer | Example Cargo |
| `parcelNumber` | Parcel | PAR-102 |
| `hatch` | Hatch | Hatch 2 |
| `position` | Position | 1 |

Source workflow: `weighbridge-web-api/WeighBridgeWeAPI/Application/ShippingOrders/ActivateShippingOrderDetail.cs`.

## rail-consignment-alert

An update has been issued for the rail consignment below.

Applications: `weighbridge-api`, `tan-webservice`.

| Variable | Meaning | Example |
|---|---|---|
| `consignmentNumber` | Consignment | CON-301 |
| `message` | Update | Wagons are ready for review. |
| `requestedBy` | Requested by | Example Operator |

Source workflow: `weighbridge-web-api/WeighBridgeWeAPI/Application/RAN/RailConsignments/NotifyAgent.cs; weighbridge-tan-webservice/Application/RAN/RailConsignments/ReceiveRailConsignment.cs`.

## weighbridge-wagon-confirmed

The wagon below has been confirmed.

Applications: `weighbridge-api`.

| Variable | Meaning | Example |
|---|---|---|
| `wagonNumber` | Wagon | WGN-104 |
| `railOrderNumber` | Rail order | RO-104 |
| `consignmentNumber` | Consignment | CON-301 |
| `customerName` | Customer | Example Cargo |
| `reason` | Decision details | See the inspection result recorded in the rail workflow. |

Source workflow: `weighbridge-web-api/WeighBridgeWeAPI/Application/RAN/RailOrderDetail/WagonConfirmation.cs`.

## weighbridge-wagon-declined

The wagon below has been declined.

Applications: `weighbridge-api`.

| Variable | Meaning | Example |
|---|---|---|
| `wagonNumber` | Wagon | WGN-104 |
| `railOrderNumber` | Rail order | RO-104 |
| `consignmentNumber` | Consignment | CON-301 |
| `customerName` | Customer | Example Cargo |
| `reason` | Decision details | See the inspection result recorded in the rail workflow. |

Source workflow: `weighbridge-web-api/WeighBridgeWeAPI/Application/RAN/RailOrderDetail/WagonDecline.cs`.

## weighbridge-equipment-approval-requested

Equipment operation details have been submitted for approval.

Applications: `weighbridge-api`.

| Variable | Meaning | Example |
|---|---|---|
| `operationId` | Operation | EO-101 |
| `vesselName` | Vessel | MV Example |
| `workOrderId` | Work order | WO-1204 |
| `submittedBy` | Submitted by | Example Operator |
| `summary` | Operation details | Review equipment allocations and operating time. |

Source workflow: `weighbridge-web-api/WeighBridgeWeAPI/Application/EquipmentOperations/EquipmentOperationDetails/SubmitEquipmentOperationDetailsToApproval.cs`.

## tan-tip-generated

The truck details have been sent to customs and a TIP has been generated.

Applications: `tan-webservice`.

| Variable | Meaning | Example |
|---|---|---|
| `registrationNumber` | Truck | AAA-123-MP |
| `tanId` | TAN | TAN-1204 |
| `orderNumber` | Order | ORD-204 |
| `transporterName` | Transporter | Example Transporter |
| `trailer1` | Trailer 1 | TRL-101 |
| `trailer2` | Trailer 2 | Not applicable |
| `mineTicketNumber` | Mine ticket | MT-204 |
| `customerName` | Cargo owner | Example Cargo |
| `declarationNumber` | Declaration | DEC-104 |

Source workflow: `weighbridge-tan-webservice/Infrastructure/Services/TipNotificationService.cs`.

## tan-tip-payment-submitted

The payment reference has been submitted to the bank for payment.

Applications: `tan-webservice`.

| Variable | Meaning | Example |
|---|---|---|
| `registrationNumber` | Truck | AAA-123-MP |
| `tanId` | TAN | TAN-1204 |
| `orderNumber` | Order | ORD-204 |
| `transporterName` | Transporter | Example Transporter |
| `trailer1` | Trailer 1 | TRL-101 |
| `trailer2` | Trailer 2 | Not applicable |
| `mineTicketNumber` | Mine ticket | MT-204 |
| `customerName` | Cargo owner | Example Cargo |
| `declarationNumber` | Declaration | DEC-104 |
| `paymentReference` | Payment reference | PAY-104 |
| `amount` | Amount | 1,250.00 |
| `currency` | Currency | MZN |
| `bankName` | Bank | Standard Bank |

Source workflow: `weighbridge-tan-webservice/Infrastructure/Services/StandardBank/StandardBankIntegratorService.cs`.

## tan-tip-payment-confirmed

The TIP payment has been confirmed by the bank.

Applications: `tan-webservice`.

| Variable | Meaning | Example |
|---|---|---|
| `registrationNumber` | Truck | AAA-123-MP |
| `tanId` | TAN | TAN-1204 |
| `orderNumber` | Order | ORD-204 |
| `transporterName` | Transporter | Example Transporter |
| `trailer1` | Trailer 1 | TRL-101 |
| `trailer2` | Trailer 2 | Not applicable |
| `mineTicketNumber` | Mine ticket | MT-204 |
| `customerName` | Cargo owner | Example Cargo |
| `declarationNumber` | Declaration | DEC-104 |
| `paymentReference` | Payment reference | PAY-104 |
| `amount` | Amount | 1,250.00 |
| `currency` | Currency | MZN |

Source workflow: `weighbridge-tan-webservice/Infrastructure/Services/TipNotificationService.cs`.

## tan-tip-payment-failed

The bank reported that the TIP payment failed.

Applications: `tan-webservice`.

| Variable | Meaning | Example |
|---|---|---|
| `registrationNumber` | Truck | AAA-123-MP |
| `tanId` | TAN | TAN-1204 |
| `orderNumber` | Order | ORD-204 |
| `transporterName` | Transporter | Example Transporter |
| `trailer1` | Trailer 1 | TRL-101 |
| `trailer2` | Trailer 2 | Not applicable |
| `mineTicketNumber` | Mine ticket | MT-204 |
| `customerName` | Cargo owner | Example Cargo |
| `declarationNumber` | Declaration | DEC-104 |
| `paymentReference` | Payment reference | PAY-104 |
| `amount` | Amount | 1,250.00 |
| `currency` | Currency | MZN |
| `reason` | Bank failure reason | The bank rejected the payment reference. |

Source workflow: `weighbridge-tan-webservice/Infrastructure/Services/TipNotificationService.cs`.

## tan-truck-release-requested

The agent has submitted a truck release. Operations approval is still required.

Applications: `tan-webservice`.

| Variable | Meaning | Example |
|---|---|---|
| `registrationNumber` | Truck | AAA-123-MP |
| `tanId` | TAN | TAN-1204 |
| `orderNumber` | Order | ORD-204 |
| `trailer1` | Trailer 1 | TRL-101 |
| `trailer2` | Trailer 2 | Not applicable |
| `customerName` | Customer | Example Cargo |
| `commodity` | Commodity | Coal |
| `destination` | Destination / slab | Terminal 1 |
| `mineTicketNumber` | Mine ticket | MT-204 |
| `mineGrossMass` | Mine gross mass (kg) | 48,000 |
| `grossMass` | First mass (kg) | 47,800 |
| `reason` | Original hold reason | Weight discrepancy requires review. |
| `agentComment` | Agent comment | Supporting documents have been provided. |
| `releasedAt` | Agent release submitted at | 09 Sep 2026 11:00 CAT |

Source workflow: `weighbridge-tan-webservice/Application/Transactions/ReleaseTruckTransaction.cs`.

## tan-cbr-vessel-change-requested

A vessel change has been requested for the cargo booking below.

Applications: `tan-webservice`.

| Variable | Meaning | Example |
|---|---|---|
| `cbrId` | Cargo booking request | CBR-104 |
| `customerName` | Customer | Example Cargo |
| `agentName` | Agent | Example Agent |
| `currentVesselName` | Current vessel | MV Example |
| `requestedVesselName` | Requested vessel | MV Replacement |
| `commodity` | Commodity | Coal |
| `parcelNumber` | Parcel | PAR-102 |
| `submittedBy` | Submitted by | Example Agent User |
| `submittedAt` | Submitted at | 09 Sep 2026 10:00 CAT |

Source workflow: `weighbridge-tan-webservice/Application/CBR/UpdateCBRVessel/UpdateCBRVessel.cs`.

## tan-sew-integration-failed

A SEW integration operation failed.

Applications: `tan-webservice`.

| Variable | Meaning | Example |
|---|---|---|
| `operation` | Operation | Declaration upload |
| `reference` | Business / file reference | TAN-1204 |
| `occurredAt` | Occurred at | 09 Sep 2026 10:00 CAT |
| `errorCode` | Error code | SEW-UPLOAD-FAILED |
| `errorSummary` | Error summary | The remote service could not be reached. |

Source workflow: `weighbridge-tan-webservice/API/Workers/SewSFTP`.

## weighbridge-ticket

Please find attached the weighbridge ticket.

Applications: `otc-api`, `weighbridge-api`, `tan-webservice`. **Attachment required.**

| Variable | Meaning | Example |
|---|---|---|
| `ticketId` | Ticket | TKT-104 |
| `ticketType` | Ticket type | Road receipt |
| `registrationNumber` | Truck | AAA-123-MP |
| `orderNumber` | Order | ORD-204 |
| `customerName` | Customer | Example Cargo |
| `issuedAt` | Issued at | 09 Sep 2026 10:00 CAT |

Source workflow: `otc-api/Infrastructure/Services/EmailService.cs; weighbridge-tan-webservice/API/Workers/SendTicketToTransporterJob.cs`.

## weighbridge-transactions-report

Please find attached the weighbridge transactions report.

Applications: `otc-api`, `weighbridge-api`, `tan-webservice`. **Attachment required.**

| Variable | Meaning | Example |
|---|---|---|
| `orderNumber` | Order | ORD-204 |
| `customerName` | Customer | Example Cargo |
| `destination` | Destination | Terminal 1 |
| `reportPeriod` | Reporting period | 01–09 Sep 2026 |
| `generatedAt` | Generated at | 09 Sep 2026 10:00 CAT |

Source workflow: `weighbridge-tan-webservice/API/Workers/SendTicketToAgentsAndCustomersJob.cs`.

## data-integrity-report

Please find attached the data integrity report.

Applications: `otc-api`, `weighbridge-api`, `tan-webservice`. **Attachment required.**

| Variable | Meaning | Example |
|---|---|---|
| `scope` | Scope | Rail orders |
| `reportPeriod` | Reporting period | 09 Sep 2026 |
| `issueCount` | Issues found | 3 |
| `summary` | Summary | Review the affected records listed in the report. |

Source workflow: `weighbridge-tan-webservice/Infrastructure/Services/DataIntegrityService.cs; weighbridge-web-api/WeighBridgeWeAPI/Application/RAN/WagonsRequest/UpdateWagonsRequestShuntingData.cs`.

## tan-trips-history-report

Please find attached the TAN trips history report for customs.

Applications: `tan-webservice`. **Attachment required.**

| Variable | Meaning | Example |
|---|---|---|
| `reportPeriod` | Reporting period | 01–09 Sep 2026 |
| `generatedAt` | Generated at | 09 Sep 2026 10:00 CAT |
| `summary` | Summary | Completed trips for the reporting period. |

Source workflow: `weighbridge-tan-webservice/API/Workers/SendTANTripsHistoryToCustomsJob.cs`.

## Example request

Authenticate as `tan-webservice` with the configured application credentials.

```json
{
  "idempotencyKey": "tan:1204:tip-payment:confirmed:PAY-104",
  "correlationId": "tan:1204",
  "businessReference": "TAN-1204",
  "to": [
    {
      "email": "transporter@example.com"
    }
  ],
  "templateId": "tan-tip-payment-confirmed",
  "variables": {
    "registrationNumber": "AAA-123-MP",
    "tanId": "TAN-1204",
    "orderNumber": "ORD-204",
    "transporterName": "Example Transporter",
    "trailer1": "TRL-101",
    "trailer2": "Not applicable",
    "mineTicketNumber": "MT-204",
    "customerName": "Example Cargo",
    "declarationNumber": "DEC-104",
    "paymentReference": "PAY-104",
    "amount": "1,250.00",
    "currency": "MZN"
  }
}
```

## Deployment and remaining work

Template files are explicitly copied to build and publish output. Apply the notification-service `RemoveTemplateVersion` migration before deploying this unversioned template implementation. It drops the obsolete template-version column; no migration has been applied automatically.

Clients remain unchanged and will not automatically switch from their inline email bodies to these templates. Wiring events, recipients, variables, attachments and producer outboxes remains client-side work for a later task. No SMS or translated variants are included. Repeated-row/batch reports should be supplied as attachments; the renderer remains a scalar placeholder renderer. Dedicated FMS, rental, induction and other less common OTC approval variants can be added separately.

The referenced Rider `console.sql` contains only `SELECT * FROM OutboxMessages`; it was read but not executed or changed.

## Verification

- 173 tests pass across the runnable suite: Application 82, Domain 17, Infrastructure 57, Persistence 9, and SQLite API integration 8.
- All 28 templates render from their documented example variables. Every required variable is individually tested for omission.
- API checks exercise each template against all three application identities, reject cross-application use, and require real document references for the six report/ticket templates. Inline resources do not satisfy the document requirement.
- Renderer tests cover HTML encoding, nonrecursive placeholder substitution, empty values, unknown template IDs, path traversal and compatibility with the existing payment template.
- Release publish succeeds; all 115 template files (including the existing payment template) were compared byte-for-byte with the published output.
- Existing SQL Server container tests remain excluded because Docker is not running. No live email sends or database updates were performed.

## Unversioned template behavior

Templates now live directly at `Api/Templates/<template-id>/` with `subject.txt`, `body.html`, `body.txt`, and optional `metadata.json`. Requests contain only the template ID and variables. Legacy incoming `templateVersion` JSON properties are ignored, and previously persisted request fingerprints remain compatible through comparison with durable request content. New outgoing events omit that property. Kafka contract/schema versions and API route versions are unchanged.

Scheduled deliveries and manual replays render the current file contents for their template ID. Updating a template therefore affects pending and replayed notifications; there is no historical template selection or rendered-content snapshot. Deploy into a clean publish directory to remove obsolete version folders.

The migration only removes `EmailNotifications.TemplateVersion`; it preserves notification content, status, IDs and outbox messages. A downgrade restores version 1 for templates and 0 for raw messages; historical version numbers cannot be recovered from the dropped column.
