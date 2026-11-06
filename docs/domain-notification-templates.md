# TAN notification templates

37 templates owned exclusively by `tan-webservice`. No TAN application code has been changed.

Submit to `POST /api/v1/notifications/email` with `Authorization: ApiKey tan-webservice.<secret>`. Supply `templateId`, `variables`, recipients, a stable `idempotencyKey` and `correlationId`. Omit raw bodies when using a template.

All documented variables are required nonempty strings. Use plain text; HTML is encoded. For lists, provide one complete record per line; the HTML layout preserves line breaks. Use `None` for empty result lists and `Not applicable` for unavailable contextual fields. Format dates with a timezone and masses in the indicated units. Never fabricate identifiers. URL fields are displayed as text, not interpolated into HTML attributes.

The service does not send attachments. `weighbridge-ticket`, `weighbridge-transactions-report`, `data-integrity-report` and `tan-trips-history-report` were written for an attached document; until their wording is revised, TAN must deliver those documents another way (for example a link variable).

## Workflow notes

- Receipt completion, weighing-time discrepancy and nett deviation are separate notifications. Keep the positive customer threshold and strict greater-than comparison in TAN.
- `weighbridge-ticket` covers both agent and transporter delivery. TAN must choose the external-storage PDF for Transfer To Port and Transfer To Vessel; templates do not generate reports or select recipients.
- `data-integrity-report` was written for an attached report; `tan-data-integrity-report-link` covers the existing report-link path for parcel, receipt, shipping and rail scopes.
- `tan-operational-failure` covers worker, SFTP, bank authentication/payment submission, Gate One, KM4, cleanup, import-notification and report-delivery failures. Use `tan-sew-integration-failed` for SEW operation errors and `tan-sew-response-errors` for returned business validation errors. Send safe failure summaries rather than credentials or full exception dumps.
- Account templates preserve the existing initial/reset-password workflow. Password values are sensitive and are persisted with notification variables; do not log them or use real passwords in examples.
- CBR submission/resubmission/closure currently delegates to INPORT. `tan-cbr-status-notification` is prepared for a future explicit integration; it does not reproduce unseen INPORT email logic.
- `tan-truck-release-requested` remains pending Operations approval. Use `tan-truck-release-operations-review` only for Operations recipients when including the Tally review address; use the original template for other recipients.
- `tan-message` covers the generic email endpoint with a plain-text message. Existing raw HTML submission remains available. `payment-confirmed` retains the basic payment contract; TIP workflows should prefer their specific templates.
- Some source jobs are disabled in startup. Templates provide content only and do not enable workers, SMS, integrations or new business events.

## Catalogue

| Template |
|---|
| `rail-consignment-alert` |
| `tan-tip-generated` |
| `tan-tip-payment-submitted` |
| `tan-tip-payment-confirmed` |
| `tan-tip-payment-failed` |
| `tan-truck-release-requested` |
| `tan-cbr-vessel-change-requested` |
| `tan-sew-integration-failed` |
| `weighbridge-ticket` |
| `weighbridge-transactions-report` |
| `data-integrity-report` |
| `tan-trips-history-report` |
| `tan-receipt-completed` |
| `tan-receipt-time-discrepancy` |
| `tan-receipt-nett-deviation` |
| `tan-data-integrity-report-link` |
| `tan-agent-allocation` |
| `tan-driver-details` |
| `tan-border-arrival-driver-conflict` |
| `tan-truck-details-missing` |
| `tan-sew-trucks-uploaded` |
| `tan-tip-batch-generated` |
| `tan-duplicate-declarations` |
| `tan-duplicate-payment-references` |
| `tan-sew-response-errors` |
| `tan-import-result` |
| `tan-operational-failure` |
| `tan-wagons-submitted` |
| `tan-wagons-updated` |
| `tan-wagons-deleted` |
| `tan-account-created` |
| `tan-account-reset` |
| `tan-agent-account-notification` |
| `tan-cbr-status-notification` |
| `tan-truck-release-operations-review` |
| `tan-message` |
| `payment-confirmed` |

## rail-consignment-alert

An update has been issued for the rail consignment below.

Source: `weighbridge-tan-webservice/Application/RAN/RailConsignments/ReceiveRailConsignment.cs`.

| Variable | Meaning | Example |
|---|---|---|
| `consignmentNumber` | Consignment | CON-301 |
| `message` | Update | Wagons are ready for review. |
| `requestedBy` | Requested by | Example Operator |

## tan-tip-generated

The truck details have been sent to customs and a TIP has been generated.

Source: `weighbridge-tan-webservice/Infrastructure/Services/TipNotificationService.cs`.

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

## tan-tip-payment-submitted

The payment reference has been submitted to the bank for payment.

Source: `weighbridge-tan-webservice/Infrastructure/Services/StandardBank/StandardBankIntegratorService.cs`.

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

## tan-tip-payment-confirmed

The TIP payment has been confirmed by the bank.

Source: `weighbridge-tan-webservice/Infrastructure/Services/TipNotificationService.cs`.

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

## tan-tip-payment-failed

The bank reported that the TIP payment failed.

Source: `weighbridge-tan-webservice/Infrastructure/Services/TipNotificationService.cs`.

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

## tan-truck-release-requested

The agent has submitted a truck release. Operations approval is still required.

Source: `weighbridge-tan-webservice/Application/Transactions/ReleaseTruckTransaction.cs`.

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

## tan-cbr-vessel-change-requested

A vessel change has been requested for the cargo booking below.

Source: `weighbridge-tan-webservice/Application/CBR/UpdateCBRVessel/UpdateCBRVessel.cs`.

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

## tan-sew-integration-failed

A SEW integration operation failed.

Source: `weighbridge-tan-webservice/API/Workers/SewSFTP`.

| Variable | Meaning | Example |
|---|---|---|
| `operation` | Operation | Declaration upload |
| `reference` | Business / file reference | TAN-1204 |
| `occurredAt` | Occurred at | 09 Sep 2026 10:00 CAT |
| `errorCode` | Error code | SEW-UPLOAD-FAILED |
| `errorSummary` | Error summary | The remote service could not be reached. |

## weighbridge-ticket

Please find attached the weighbridge ticket.

Source: `weighbridge-tan-webservice/Infrastructure/Services/ReceiptTransactionNotification.cs`.

| Variable | Meaning | Example |
|---|---|---|
| `ticketId` | Ticket | TKT-104 |
| `ticketType` | Ticket type | Road receipt |
| `registrationNumber` | Truck | AAA-123-MP |
| `orderNumber` | Order | ORD-204 |
| `customerName` | Customer | Example Cargo |
| `issuedAt` | Issued at | 09 Sep 2026 10:00 CAT |

## weighbridge-transactions-report

Please find attached the weighbridge transactions report.

Source: `weighbridge-tan-webservice/API/Workers/SendTicketToAgentsAndCustomersJob.cs`.

| Variable | Meaning | Example |
|---|---|---|
| `orderNumber` | Order | ORD-204 |
| `customerName` | Customer | Example Cargo |
| `destination` | Destination | Terminal 1 |
| `reportPeriod` | Reporting period | 01–09 Sep 2026 |
| `generatedAt` | Generated at | 09 Sep 2026 10:00 CAT |

## data-integrity-report

Please find attached the data integrity report.

Source: `weighbridge-tan-webservice/Infrastructure/Services/DataIntegrityService.cs`.

| Variable | Meaning | Example |
|---|---|---|
| `scope` | Scope | Rail orders |
| `reportPeriod` | Reporting period | 09 Sep 2026 |
| `issueCount` | Issues found | 3 |
| `summary` | Summary | Review the affected records listed in the report. |

## tan-trips-history-report

Please find attached the TAN trips history report for customs.

Source: `weighbridge-tan-webservice/API/Workers/SendTANTripsHistoryToCustomsJob.cs`.

| Variable | Meaning | Example |
|---|---|---|
| `reportPeriod` | Reporting period | 01–09 Sep 2026 |
| `generatedAt` | Generated at | 09 Sep 2026 10:00 CAT |
| `summary` | Summary | Completed trips for the reporting period. |

## tan-receipt-completed

The truck has completed its receipt transaction.

Source: `weighbridge-tan-webservice/Infrastructure/Services/ReceiptTransactionNotification.cs`.

| Variable | Meaning | Example |
|---|---|---|
| `transactionId` | Receipt transaction | 12345 |
| `orderNumber` | Order | ORD-204 |
| `customerName` | Customer | Example Cargo |
| `commodity` | Commodity | Coal |
| `parcelNumber` | Parcel | PAR-102 |
| `destination` | Destination | Terminal 1 |
| `transporterName` | Transporter | Example Transporter |
| `registrationNumber` | Truck | AAA-123-MP |
| `trailer1` | Trailer 1 | TRL-101 |
| `trailer2` | Trailer 2 | Not applicable |
| `mineMassKg` | Mine mass (kg) | 30000 |
| `mineTicketNumber` | Mine ticket | MT-204 |
| `entryAt` | First weighing at | 11 Sep 2026 10:00 CAT |
| `exitAt` | Second weighing at | 11 Sep 2026 10:30 CAT |
| `grossKg` | Gross mass (kg) | 48000 |
| `tareKg` | Tare mass (kg) | 18500 |
| `nettKg` | Nett mass (kg) | 29500 |

## tan-receipt-time-discrepancy

The time between first and second weighing is below the configured minimum. Please review this transaction.

Source: `weighbridge-tan-webservice/Infrastructure/Services/ReceiptTransactionNotification.cs`.

| Variable | Meaning | Example |
|---|---|---|
| `transactionId` | Receipt transaction | 12345 |
| `orderNumber` | Order | ORD-204 |
| `customerName` | Customer | Example Cargo |
| `commodity` | Commodity | Coal |
| `parcelNumber` | Parcel | PAR-102 |
| `destination` | Destination | Terminal 1 |
| `transporterName` | Transporter | Example Transporter |
| `registrationNumber` | Truck | AAA-123-MP |
| `trailer1` | Trailer 1 | TRL-101 |
| `trailer2` | Trailer 2 | Not applicable |
| `mineMassKg` | Mine mass (kg) | 30000 |
| `mineTicketNumber` | Mine ticket | MT-204 |
| `entryAt` | First weighing at | 11 Sep 2026 10:00 CAT |
| `exitAt` | Second weighing at | 11 Sep 2026 10:30 CAT |
| `grossKg` | Gross mass (kg) | 48000 |
| `tareKg` | Tare mass (kg) | 18500 |
| `nettKg` | Nett mass (kg) | 29500 |
| `elapsedMinutes` | Elapsed time (minutes) | 3 |
| `minimumMinutes` | Minimum time (minutes) | 5 |

## tan-receipt-nett-deviation

The absolute difference between mine mass and nett mass exceeds the customer notification threshold.

Source: `weighbridge-tan-webservice/Infrastructure/Services/ReceiptTransactionNotification.cs`.

| Variable | Meaning | Example |
|---|---|---|
| `transactionId` | Receipt transaction | 12345 |
| `orderNumber` | Order | ORD-204 |
| `customerName` | Customer | Example Cargo |
| `commodity` | Commodity | Coal |
| `parcelNumber` | Parcel | PAR-102 |
| `destination` | Destination | Terminal 1 |
| `transporterName` | Transporter | Example Transporter |
| `registrationNumber` | Truck | AAA-123-MP |
| `trailer1` | Trailer 1 | TRL-101 |
| `trailer2` | Trailer 2 | Not applicable |
| `mineMassKg` | Mine mass (kg) | 30000 |
| `mineTicketNumber` | Mine ticket | MT-204 |
| `entryAt` | First weighing at | 11 Sep 2026 10:00 CAT |
| `exitAt` | Second weighing at | 11 Sep 2026 10:30 CAT |
| `grossKg` | Gross mass (kg) | 48000 |
| `tareKg` | Tare mass (kg) | 18500 |
| `nettKg` | Nett mass (kg) | 29500 |
| `deviationKg` | Absolute deviation (kg) | 500 |
| `thresholdKg` | Customer threshold (kg) | 300 |

## tan-data-integrity-report-link

The data integrity results are available at the report address below.

Source: `weighbridge-tan-webservice/Infrastructure/Services/DataIntegrityService.cs`.

| Variable | Meaning | Example |
|---|---|---|
| `scope` | Scope and order or parcel reference | Receipt order ORD-204 |
| `reportUrl` | Report address | https://reports.example.com/integrity/204 |

## tan-agent-allocation

A TAN has been allocated to your console. Please allocate the correct order.

Source: `weighbridge-tan-webservice/Application/Tan/ChangeTanAgent.cs`.

| Variable | Meaning | Example |
|---|---|---|
| `tanId` | TAN | TAN-1204 |
| `agentName` | Receiving agent | Example Agent |
| `previousAgentName` | Allocating agent | Previous Agent |
| `registrationNumber` | Truck | AAA-123-MP |
| `trailer1` | Trailer 1 | TRL-101 |
| `trailer2` | Trailer 2 | Not applicable |

## tan-driver-details

Please review the driver details and existing valid declarations for this truck.

Source: `weighbridge-tan-webservice/Infrastructure/Services/TanService.cs`.

| Variable | Meaning | Example |
|---|---|---|
| `tanId` | TAN | TAN-1204 |
| `registrationNumber` | Truck | AAA-123-MP |
| `transporterName` | Transporter | Example Transporter |
| `driverName` | Driver | Example Driver |
| `driverPassport` | Driver passport | EXAMPLE-123 |
| `driverContact` | Driver contact | Not provided |
| `truckDetails` | Trailer, order and customer details | Trailer TRL-101; order ORD-204; customer Example Cargo |
| `validDeclarations` | Valid declarations and expiry dates | DEC-104; expires 20 Sep 2026 |

## tan-border-arrival-driver-conflict

Border arrival has been confirmed. The same driver is associated with other TAN records; review the details below.

Source: `weighbridge-tan-webservice/Application/Tan/UpdateTanBorderArrivalTime.cs`.

| Variable | Meaning | Example |
|---|---|---|
| `tanId` | TAN | TAN-1204 |
| `registrationNumber` | Arriving truck | AAA-123-MP |
| `borderArrivalAt` | Border arrival at | 11 Sep 2026 10:00 CAT |
| `driverName` | Driver | Example Driver |
| `driverPassport` | Driver passport | EXAMPLE-123 |
| `otherTrucks` | Other TANs and truck details | TAN-1203; BBB-456-MP |

## tan-truck-details-missing

The truck has arrived at the border, but its details could not be found for TIP generation. Send the truck details to MPDC or update them in TAN.

Source: `weighbridge-tan-webservice/Infrastructure/Services/SewTanUploaderService.cs`.

| Variable | Meaning | Example |
|---|---|---|
| `registrationNumber` | Truck | AAA-123-MP |
| `borderArrivalAt` | Border arrival at | 11 Sep 2026 10:00 CAT |

## tan-sew-trucks-uploaded

The following truck details have been uploaded to SEW for customs processing.

Source: `weighbridge-tan-webservice/Infrastructure/Services/SewTanUploaderService.cs`.

| Variable | Meaning | Example |
|---|---|---|
| `transporterName` | Transporter | Example Transporter |
| `truckDetails` | TAN, truck, trailer, order and driver details | TAN-1204; AAA-123-MP; TRL-101; ORD-204; Example Driver |

## tan-tip-batch-generated

Review the generated TIP declarations and payment instructions below.

Source: `weighbridge-tan-webservice/Infrastructure/Services/TipNotificationService.cs; weighbridge-tan-webservice/Application/Sew/Payments/PostSewIntegrationPayment.cs`.

| Variable | Meaning | Example |
|---|---|---|
| `transporterName` | Transporter | Example Transporter |
| `paymentInstructions` | Payment requirement and instructions | Payment required; use the reference below. |
| `tipDetails` | TANs, trucks, declarations, payment references, amounts and expiry dates | TAN-1204; AAA-123-MP; DEC-104; PAY-104; MZN 1250.00; expires 20 Sep 2026 |

## tan-duplicate-declarations

Duplicate records were received. Please review the affected records before further processing.

Source: `weighbridge-tan-webservice/API/Workers/SewSFTP/SewSuccessWorker.cs`.

| Variable | Meaning | Example |
|---|---|---|
| `sourceReference` | Source file or request reference | incoming-104.xml |
| `duplicateDetails` | Duplicate identifiers and associated truck, TAN and declaration details | TAN-1204; AAA-123-MP; DEC-104; PAY-104 |

## tan-duplicate-payment-references

Duplicate records were received. Please review the affected records before further processing.

Source: `weighbridge-tan-webservice/API/Workers/SewSFTP/SewDeclarationWorker.cs; weighbridge-tan-webservice/Application/Sew/Payments/PostSewIntegrationPayment.cs`.

| Variable | Meaning | Example |
|---|---|---|
| `sourceReference` | Source file or request reference | incoming-104.xml |
| `duplicateDetails` | Duplicate identifiers and associated truck, TAN and declaration details | TAN-1204; AAA-123-MP; DEC-104; PAY-104 |

## tan-sew-response-errors

SEW returned errors for the records below. Review and correct the affected truck details.

Source: `weighbridge-tan-webservice/API/Workers/SewSFTP/SewIntegrationErrorsDownloadWorker.cs`.

| Variable | Meaning | Example |
|---|---|---|
| `fileName` | Response file | response-104.xml |
| `errorDetails` | Errors and associated TAN, truck and transporter details | TAN-1204; AAA-123-MP; invalid declaration details |

## tan-import-result

The import has completed with the results below. Review unsuccessful records before submitting them again.

Source: `weighbridge-tan-webservice/Infrastructure/Services/TanService.cs`.

| Variable | Meaning | Example |
|---|---|---|
| `fileName` | Import file | tans-20260911.xlsx |
| `successDetails` | Successfully imported records | TAN-1204; AAA-123-MP |
| `errorDetails` | Rejected records and reasons | Row 3: missing order number |

## tan-operational-failure

An operation could not be completed. Please investigate the details below.

Source: `weighbridge-tan-webservice/API/Workers; weighbridge-tan-webservice/Infrastructure/Services/GateOneService.cs; weighbridge-tan-webservice/Infrastructure/Services/Sftp/SftpService.cs; weighbridge-tan-webservice/Infrastructure/Services/DeleteLocalFile/DeleteLocalFileService.cs; weighbridge-tan-webservice/Infrastructure/Services/Km4and7/Km4And7ProcessService.cs; weighbridge-tan-webservice/Infrastructure/Services/StandardBank/StandardBankIntegratorService.cs`.

| Variable | Meaning | Example |
|---|---|---|
| `operation` | Worker or operation | Receipt ticket delivery |
| `reference` | Affected business or file reference | Receipt 12345 |
| `errorSummary` | Failure details safe for email | The report server could not be reached. |

## tan-wagons-submitted

Wagons have been submitted for the rail order below.

Source: `weighbridge-tan-webservice/Application/RAN/SubmitWagons.cs`.

| Variable | Meaning | Example |
|---|---|---|
| `orderNumber` | Rail order | RAIL-204 |
| `consignmentNumber` | Consignment | CON-104 |
| `wagonDetails` | Wagon numbers, cargo, customer, masses and operation details | WG-101; Coal; Example Cargo; nett 60000 kg |
| `changedBy` | Submitted or changed by | Example Agent |

## tan-wagons-updated

A wagon on the consignment has been updated. Review the current details below.

Source: `weighbridge-tan-webservice/Application/RAN/UpdateRailOrderDetail.cs`.

| Variable | Meaning | Example |
|---|---|---|
| `orderNumber` | Rail order | RAIL-204 |
| `consignmentNumber` | Consignment | CON-104 |
| `wagonDetails` | Wagon numbers, cargo, customer, masses and operation details | WG-101; Coal; Example Cargo; nett 60000 kg |
| `changedBy` | Submitted or changed by | Example Agent |

## tan-wagons-deleted

A wagon has been removed from the consignment.

Source: `weighbridge-tan-webservice/Application/RAN/DeleteRailOrderDetail.cs`.

| Variable | Meaning | Example |
|---|---|---|
| `orderNumber` | Rail order | RAIL-204 |
| `consignmentNumber` | Consignment | CON-104 |
| `wagonDetails` | Wagon numbers, cargo, customer, masses and operation details | WG-101; Coal; Example Cargo; nett 60000 kg |
| `changedBy` | Submitted or changed by | Example Agent |

## tan-account-created

Your MPDC Cargo Booking account has been created. Change your password after logging in.

Source: `weighbridge-tan-webservice/Infrastructure/Services/UserService.cs`.

| Variable | Meaning | Example |
|---|---|---|
| `fullName` | Account holder | Example User |
| `username` | Username | example.user |
| `password` | Initial or reset password (sensitive) | EXAMPLE-ONLY-ChangeMe |
| `loginUrl` | Cargo Booking address | https://tan.portmaputo.com |

## tan-account-reset

Your MPDC Cargo Booking account has been reset. Change your password after logging in.

Source: `weighbridge-tan-webservice/Infrastructure/Services/UserService.cs`.

| Variable | Meaning | Example |
|---|---|---|
| `fullName` | Account holder | Example User |
| `username` | Username | example.user |
| `password` | Initial or reset password (sensitive) | EXAMPLE-ONLY-ChangeMe |
| `loginUrl` | Cargo Booking address | https://tan.portmaputo.com |

## tan-agent-account-notification

Your MPDC Cargo Booking account details are below. Change your password after logging in.

Source: `weighbridge-tan-webservice/Application/User/NotifyAgentAccounts.cs`.

| Variable | Meaning | Example |
|---|---|---|
| `fullName` | Account holder | Example User |
| `username` | Username | example.user |
| `password` | Initial or reset password (sensitive) | EXAMPLE-ONLY-ChangeMe |
| `loginUrl` | Cargo Booking address | https://tan.portmaputo.com |

## tan-cbr-status-notification

Review the cargo booking request status below.

Source: `weighbridge-tan-webservice/Infrastructure/Services/EmailService.cs; weighbridge-tan-webservice/Infrastructure/Cbr/CbrService.cs`.

| Variable | Meaning | Example |
|---|---|---|
| `cbrId` | Cargo booking request | CBR-104 |
| `status` | Booking status | Submitted |
| `customerName` | Customer | Example Cargo |
| `bookingDetails` | Vessel, cargo, quantity and booking details | MV Example; Coal; 1000 tonnes |
| `nextSteps` | Required follow-up | Review the booking in Cargo Booking. |

## tan-message

Please review the message below.

Source: `weighbridge-tan-webservice/API/Controllers/EmailController.cs`.

| Variable | Meaning | Example |
|---|---|---|
| `title` | Message title | TAN notification |
| `message` | Message in plain text | Please review TAN-1204. |

## payment-confirmed

Your payment has been confirmed.

Source: `weighbridge-tan-webservice/Infrastructure/Services/TipNotificationService.cs`.

| Variable | Meaning | Example |
|---|---|---|
| `customerName` | Customer | Example Cargo |
| `paymentReference` | Payment reference | PAY-104 |

## tan-truck-release-operations-review

Operations-only version of `tan-truck-release-requested`. Uses the same variables plus required `tallyUrl` (the Operations review address, displayed as text). The truck remains blocked pending an Operations decision.

Source: `weighbridge-tan-webservice/Application/Transactions/ReleaseTruckTransaction.cs`.
