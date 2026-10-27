using System;
using System.Collections.Generic;

namespace Pohoda.Contract.Models;

/// <summary>
/// One line item read back from an existing příjemka in Pohoda. Carries price/discount/unit (not
/// just quantity) so that an <see cref="Services.IReceiptService.UpdateAsync"/> full-document resend
/// can reproduce existing lines byte-for-byte instead of risking overwriting them with zeros.
/// </summary>
/// <param name="HasUnparsedNumbers">
/// True when at least one of <paramref name="Quantity"/>, <paramref name="UnitPrice"/> or
/// <paramref name="DiscountPercent"/> could not be parsed from the mServer response — the value on
/// this line is a substituted <c>0</c> rather than a real figure. Defaults to <c>false</c> so every
/// existing construction site stays valid.
/// </param>
public sealed record ExistingReceiptLine(string PohodaStockCode, string Text, string Unit, decimal Quantity, decimal UnitPrice, decimal DiscountPercent, bool HasUnparsedNumbers = false);

/// <summary>An existing příjemka document as returned by <see cref="Services.IReceiptService.ListRecentAsync"/>.</summary>
/// <param name="Date">
/// The příjemka's date as parsed from the mServer response's <c>pri:date</c>, or <c>default</c>
/// (0001-01-01) when the response carried no parsable date. This value is informational today:
/// <see cref="Services.IReceiptService.UpdateAsync"/> does not yet preserve it and instead stamps
/// today's date into an updated receipt (see the open item in the spec).
/// </param>
public sealed record ExistingReceipt(string PohodaId, string Number, string Text, IReadOnlyList<ExistingReceiptLine> Lines, DateOnly Date = default);

/// <summary>One line to write into a new or updated příjemka.</summary>
public sealed record ReceiptLineToWrite(string PohodaStockCode, string Text, decimal Quantity, string Unit, decimal UnitPrice, decimal DiscountPercent);
