using System.Globalization;
using System.Net;
using OnCallendar.Application.Common.Interfaces;
using OnCallendar.Domain.Notifications;

namespace OnCallendar.Infrastructure.Notifications;

/// <summary>
/// Genera testi (titolo, push, mail HTML) per ciascun tipo di notifica
/// in italiano e inglese. Le stringhe sono in code per semplicità (no Razor):
/// l'aspetto grafico HTML è centralizzato in <see cref="EmailLayout"/>.
/// </summary>
public sealed class NotificationTemplateRenderer : INotificationTemplateRenderer
{
    public RenderedNotification Render(
        string type,
        string locale,
        IReadOnlyDictionary<string, string?> data,
        string? deepLinkUrl)
    {
        var lang = (locale ?? "it").Trim().ToLowerInvariant();
        if (lang != "en") lang = "it";

        var s = lang == "en" ? EnStrings(type, data) : ItStrings(type, data);

        var html = EmailLayout.Wrap(
            title: s.EmailHeading,
            preheader: s.ShortMessage,
            bodyHtml: s.EmailBodyHtml,
            ctaLabel: s.CtaLabel,
            ctaUrl: deepLinkUrl,
            footerHint: lang == "en"
                ? "You can manage your notification preferences in the app under Settings → Notifications."
                : "Puoi gestire le notifiche dall'app: Impostazioni → Notifiche.",
            locale: lang);

        var text = HtmlToText(s.EmailBodyHtml) +
                   (string.IsNullOrEmpty(deepLinkUrl) ? string.Empty : $"\n\n→ {deepLinkUrl}");

        return new RenderedNotification(
            Title: s.Title,
            ShortMessage: s.ShortMessage,
            EmailSubject: s.EmailSubject,
            EmailHtmlBody: html,
            EmailTextBody: text,
            PushTitle: s.PushTitle,
            PushBody: s.PushBody);
    }

    // -------------------------------------------------------------------
    private sealed record TplStrings(
        string Title,
        string ShortMessage,
        string EmailSubject,
        string EmailHeading,
        string EmailBodyHtml,
        string CtaLabel,
        string PushTitle,
        string PushBody);

    private static string Get(IReadOnlyDictionary<string, string?> data, string key, string fallback = "")
        => data.TryGetValue(key, out var v) ? (v ?? fallback) : fallback;

    private static string E(string s) => WebUtility.HtmlEncode(s ?? string.Empty);

    // ===================================================================
    //                              ITALIANO
    // ===================================================================
    private static TplStrings ItStrings(string type, IReadOnlyDictionary<string, string?> d)
    {
        var initiator = E(Get(d, "InitiatorName"));
        var recipient = E(Get(d, "RecipientFirstName", Get(d, "RecipientName")));
        var shiftCode = E(Get(d, "ShiftCode"));
        var shiftDate = E(Get(d, "ShiftDate"));
        var otherShiftCode = E(Get(d, "OtherShiftCode"));
        var otherShiftDate = E(Get(d, "OtherShiftDate"));
        var msg = Get(d, "Message");
        var msgHtml = string.IsNullOrWhiteSpace(msg)
            ? string.Empty
            : $"<p style=\"margin:14px 0 0 0;padding:12px 14px;background:#F2F6FA;border-left:3px solid #355872;border-radius:4px;color:#2D4254;\">«{E(msg)}»</p>";
        var reason = Get(d, "Reason");
        var reasonHtml = string.IsNullOrWhiteSpace(reason)
            ? string.Empty
            : $"<p style=\"margin:14px 0 0 0;color:#5A6B7A;\"><i>Motivo:</i> {E(reason)}</p>";

        switch (type)
        {
            case NotificationTypeCodes.SwapRequested:
            {
                var isSwap = Get(d, "RequestKind") == "Swap";
                var title = isSwap ? "Nuova proposta di scambio" : "Nuova proposta di cessione";
                var body = isSwap
                    ? $"<p>Ciao <b>{recipient}</b>,</p><p><b>{initiator}</b> ti propone di scambiare il suo turno <b>{shiftCode}</b> del <b>{shiftDate}</b> con il tuo <b>{otherShiftCode}</b> del <b>{otherShiftDate}</b>.</p>{msgHtml}<p style=\"margin-top:18px;\">Apri OnCallendar per accettare, rifiutare o proporre una controproposta.</p>"
                    : $"<p>Ciao <b>{recipient}</b>,</p><p><b>{initiator}</b> ti ha proposto di prendere il suo turno <b>{shiftCode}</b> del <b>{shiftDate}</b>.</p>{msgHtml}<p style=\"margin-top:18px;\">Apri OnCallendar per accettare o rifiutare.</p>";
                return new TplStrings(
                    Title: title,
                    ShortMessage: isSwap ? $"{Get(d,"InitiatorName")} ti propone uno scambio per il {shiftDate}" : $"{Get(d,"InitiatorName")} ti propone di prendere il turno {shiftCode} del {shiftDate}",
                    EmailSubject: $"[OnCallendar] {title} da {Get(d,"InitiatorName")}",
                    EmailHeading: title,
                    EmailBodyHtml: body,
                    CtaLabel: "Apri richiesta",
                    PushTitle: title,
                    PushBody: isSwap ? $"{Get(d,"InitiatorName")} → scambio {shiftCode} del {shiftDate}" : $"{Get(d,"InitiatorName")} → ti cede {shiftCode} del {shiftDate}");
            }

            case NotificationTypeCodes.SwapAccepted:
                return new TplStrings(
                    Title: "Richiesta accettata",
                    ShortMessage: $"{Get(d,"CounterpartName")} ha accettato il turno {shiftCode} del {shiftDate}",
                    EmailSubject: $"[OnCallendar] {Get(d,"CounterpartName")} ha accettato la tua richiesta",
                    EmailHeading: "Richiesta accettata ✓",
                    EmailBodyHtml: $"<p>Ciao <b>{recipient}</b>,</p><p>Buone notizie: <b>{E(Get(d,"CounterpartName"))}</b> ha accettato la tua richiesta sul turno <b>{shiftCode}</b> del <b>{shiftDate}</b>.</p><p style=\"margin-top:18px;\">Il calendario è stato aggiornato. Apri OnCallendar per vedere il dettaglio.</p>",
                    CtaLabel: "Apri calendario",
                    PushTitle: "Richiesta accettata",
                    PushBody: $"{Get(d,"CounterpartName")} → {shiftCode} del {shiftDate}");

            case NotificationTypeCodes.SwapRejected:
                return new TplStrings(
                    Title: "Richiesta rifiutata",
                    ShortMessage: $"{Get(d,"CounterpartName")} ha rifiutato il turno del {shiftDate}",
                    EmailSubject: $"[OnCallendar] {Get(d,"CounterpartName")} ha rifiutato la tua richiesta",
                    EmailHeading: "Richiesta rifiutata",
                    EmailBodyHtml: $"<p>Ciao <b>{recipient}</b>,</p><p><b>{E(Get(d,"CounterpartName"))}</b> ha rifiutato la tua richiesta sul turno <b>{shiftCode}</b> del <b>{shiftDate}</b>.</p>{reasonHtml}<p style=\"margin-top:18px;\">Puoi riproporlo a un altro collega o pubblicarlo in bacheca.</p>",
                    CtaLabel: "Apri OnCallendar",
                    PushTitle: "Richiesta rifiutata",
                    PushBody: $"{Get(d,"CounterpartName")} → {shiftCode} del {shiftDate}");

            case NotificationTypeCodes.SwapCancelled:
                return new TplStrings(
                    Title: "Richiesta annullata",
                    ShortMessage: $"{Get(d,"InitiatorName")} ha annullato la richiesta del {shiftDate}",
                    EmailSubject: $"[OnCallendar] Richiesta annullata da {Get(d,"InitiatorName")}",
                    EmailHeading: "Richiesta annullata",
                    EmailBodyHtml: $"<p>Ciao <b>{recipient}</b>,</p><p><b>{initiator}</b> ha annullato la richiesta sul turno <b>{shiftCode}</b> del <b>{shiftDate}</b>.</p><p style=\"margin-top:18px;\">Non devi fare nulla.</p>",
                    CtaLabel: "Apri OnCallendar",
                    PushTitle: "Richiesta annullata",
                    PushBody: $"{Get(d,"InitiatorName")} → {shiftCode} del {shiftDate}");

            case NotificationTypeCodes.CounterOfferReceived:
                return new TplStrings(
                    Title: "Controproposta ricevuta",
                    ShortMessage: $"{Get(d,"CounterpartName")} propone {otherShiftCode} del {otherShiftDate}",
                    EmailSubject: $"[OnCallendar] Controproposta da {Get(d,"CounterpartName")}",
                    EmailHeading: "Controproposta ricevuta",
                    EmailBodyHtml: $"<p>Ciao <b>{recipient}</b>,</p><p><b>{E(Get(d,"CounterpartName"))}</b> ti propone di prendere il tuo turno <b>{shiftCode}</b> del <b>{shiftDate}</b> in cambio del suo <b>{otherShiftCode}</b> del <b>{otherShiftDate}</b>.</p>{msgHtml}<p style=\"margin-top:18px;\">Apri OnCallendar per accettare o rifiutare.</p>",
                    CtaLabel: "Vedi controproposta",
                    PushTitle: "Controproposta ricevuta",
                    PushBody: $"{Get(d,"CounterpartName")} → {otherShiftCode} del {otherShiftDate}");

            case NotificationTypeCodes.CounterOfferAccepted:
                return new TplStrings(
                    Title: "Controproposta accettata",
                    ShortMessage: $"La tua controproposta del {shiftDate} è stata accettata",
                    EmailSubject: "[OnCallendar] Controproposta accettata",
                    EmailHeading: "Controproposta accettata ✓",
                    EmailBodyHtml: $"<p>Ciao <b>{recipient}</b>,</p><p>La tua controproposta sul turno <b>{shiftCode}</b> del <b>{shiftDate}</b> è stata <b>accettata</b>. I turni sono stati scambiati.</p>",
                    CtaLabel: "Apri calendario",
                    PushTitle: "Controproposta accettata",
                    PushBody: $"{shiftCode} del {shiftDate}");

            case NotificationTypeCodes.CounterOfferRejected:
                return new TplStrings(
                    Title: "Controproposta rifiutata",
                    ShortMessage: $"La tua controproposta del {shiftDate} è stata rifiutata",
                    EmailSubject: "[OnCallendar] Controproposta rifiutata",
                    EmailHeading: "Controproposta rifiutata",
                    EmailBodyHtml: $"<p>Ciao <b>{recipient}</b>,</p><p>La tua controproposta sul turno <b>{shiftCode}</b> del <b>{shiftDate}</b> è stata rifiutata.</p>{reasonHtml}",
                    CtaLabel: "Apri OnCallendar",
                    PushTitle: "Controproposta rifiutata",
                    PushBody: $"{shiftCode} del {shiftDate}");

            case NotificationTypeCodes.ShiftAssigned:
                return new TplStrings(
                    Title: "Nuovo turno assegnato",
                    ShortMessage: $"Sei stato assegnato al turno {shiftCode} del {shiftDate}",
                    EmailSubject: $"[OnCallendar] Nuovo turno: {shiftCode} del {shiftDate}",
                    EmailHeading: "Nuovo turno assegnato",
                    EmailBodyHtml: $"<p>Ciao <b>{recipient}</b>,</p><p>Ti è stato assegnato un nuovo turno: <b>{shiftCode}</b> del <b>{shiftDate}</b>.</p><p style=\"margin-top:18px;\">Apri OnCallendar per vederne il dettaglio.</p>",
                    CtaLabel: "Apri calendario",
                    PushTitle: "Nuovo turno",
                    PushBody: $"{shiftCode} del {shiftDate}");

            case NotificationTypeCodes.ShiftReassigned:
                return new TplStrings(
                    Title: "Turno modificato",
                    ShortMessage: $"Il tuo turno {shiftCode} del {shiftDate} è stato modificato",
                    EmailSubject: $"[OnCallendar] Turno modificato: {shiftCode} del {shiftDate}",
                    EmailHeading: "Turno modificato",
                    EmailBodyHtml: $"<p>Ciao <b>{recipient}</b>,</p><p>Il tuo turno <b>{shiftCode}</b> del <b>{shiftDate}</b> è stato modificato.</p><p style=\"margin-top:18px;\">Apri OnCallendar per vedere le nuove informazioni.</p>",
                    CtaLabel: "Apri calendario",
                    PushTitle: "Turno modificato",
                    PushBody: $"{shiftCode} del {shiftDate}");

            case NotificationTypeCodes.ShiftRemoved:
                return new TplStrings(
                    Title: "Turno rimosso",
                    ShortMessage: $"Il tuo turno {shiftCode} del {shiftDate} è stato rimosso",
                    EmailSubject: $"[OnCallendar] Turno rimosso: {shiftCode} del {shiftDate}",
                    EmailHeading: "Turno rimosso",
                    EmailBodyHtml: $"<p>Ciao <b>{recipient}</b>,</p><p>Il tuo turno <b>{shiftCode}</b> del <b>{shiftDate}</b> è stato rimosso dal calendario.</p>",
                    CtaLabel: "Apri calendario",
                    PushTitle: "Turno rimosso",
                    PushBody: $"{shiftCode} del {shiftDate}");

            case NotificationTypeCodes.ShiftPostedToBoard:
                return new TplStrings(
                    Title: "Turno disponibile in bacheca",
                    ShortMessage: $"Disponibile in bacheca: {shiftCode} del {shiftDate}",
                    EmailSubject: $"[OnCallendar] Turno in bacheca: {shiftCode} del {shiftDate}",
                    EmailHeading: "Nuovo turno in bacheca",
                    EmailBodyHtml: $"<p>Ciao <b>{recipient}</b>,</p><p><b>{initiator}</b> ha pubblicato in bacheca il turno <b>{shiftCode}</b> del <b>{shiftDate}</b>: chi se lo prende?</p>",
                    CtaLabel: "Apri bacheca",
                    PushTitle: "Turno in bacheca",
                    PushBody: $"{shiftCode} del {shiftDate}");

            case NotificationTypeCodes.ExternalDoctorAssigned:
                return new TplStrings(
                    Title: "Turno affidato a medico esterno",
                    ShortMessage: $"Il tuo turno {shiftCode} del {shiftDate} è stato affidato a un medico esterno",
                    EmailSubject: $"[OnCallendar] Turno affidato a medico esterno",
                    EmailHeading: "Turno affidato a un medico esterno",
                    EmailBodyHtml: $"<p>Ciao <b>{recipient}</b>,</p><p>Il tuo turno <b>{shiftCode}</b> del <b>{shiftDate}</b> è stato affidato a <b>{E(Get(d,"ExternalDoctorName"))}</b>.</p>",
                    CtaLabel: "Apri calendario",
                    PushTitle: "Turno affidato",
                    PushBody: $"{shiftCode} del {shiftDate} → {Get(d,"ExternalDoctorName")}");

            case NotificationTypeCodes.ReminderShiftTomorrow:
                return new TplStrings(
                    Title: "Promemoria turno",
                    ShortMessage: $"Domani sei di turno: {shiftCode} del {shiftDate}",
                    EmailSubject: $"[OnCallendar] Promemoria: domani sei di turno",
                    EmailHeading: "Domani sei di turno",
                    EmailBodyHtml: $"<p>Ciao <b>{recipient}</b>,</p><p>Promemoria: domani <b>{shiftDate}</b> sei di turno (<b>{shiftCode}</b>).</p>",
                    CtaLabel: "Apri calendario",
                    PushTitle: "Domani sei di turno",
                    PushBody: $"{shiftCode} del {shiftDate}");

            case NotificationTypeCodes.ReminderOnCallToday:
                return new TplStrings(
                    Title: "Promemoria reperibilità",
                    ShortMessage: $"Oggi sei di reperibilità ({shiftDate})",
                    EmailSubject: $"[OnCallendar] Oggi sei di reperibilità",
                    EmailHeading: "Oggi sei di reperibilità",
                    EmailBodyHtml: $"<p>Ciao <b>{recipient}</b>,</p><p>Promemoria: oggi <b>{shiftDate}</b> sei in reperibilità.</p>",
                    CtaLabel: "Apri calendario",
                    PushTitle: "Reperibilità oggi",
                    PushBody: shiftDate);

            case NotificationTypeCodes.SystemAnnouncement:
            default:
                return new TplStrings(
                    Title: Get(d, "Title", "Comunicazione"),
                    ShortMessage: Get(d, "Body", string.Empty),
                    EmailSubject: $"[OnCallendar] {Get(d, "Title", "Comunicazione")}",
                    EmailHeading: Get(d, "Title", "Comunicazione"),
                    EmailBodyHtml: $"<p>{E(Get(d, "Body"))}</p>",
                    CtaLabel: "Apri OnCallendar",
                    PushTitle: Get(d, "Title", "OnCallendar"),
                    PushBody: Get(d, "Body", string.Empty));
        }
    }

    // ===================================================================
    //                              ENGLISH
    // ===================================================================
    private static TplStrings EnStrings(string type, IReadOnlyDictionary<string, string?> d)
    {
        var initiator = E(Get(d, "InitiatorName"));
        var recipient = E(Get(d, "RecipientFirstName", Get(d, "RecipientName")));
        var shiftCode = E(Get(d, "ShiftCode"));
        var shiftDate = E(Get(d, "ShiftDate"));
        var otherShiftCode = E(Get(d, "OtherShiftCode"));
        var otherShiftDate = E(Get(d, "OtherShiftDate"));
        var msg = Get(d, "Message");
        var msgHtml = string.IsNullOrWhiteSpace(msg)
            ? string.Empty
            : $"<p style=\"margin:14px 0 0 0;padding:12px 14px;background:#F2F6FA;border-left:3px solid #355872;border-radius:4px;color:#2D4254;\">«{E(msg)}»</p>";
        var reason = Get(d, "Reason");
        var reasonHtml = string.IsNullOrWhiteSpace(reason)
            ? string.Empty
            : $"<p style=\"margin:14px 0 0 0;color:#5A6B7A;\"><i>Reason:</i> {E(reason)}</p>";

        switch (type)
        {
            case NotificationTypeCodes.SwapRequested:
            {
                var isSwap = Get(d, "RequestKind") == "Swap";
                var title = isSwap ? "New swap proposal" : "New shift handover";
                var body = isSwap
                    ? $"<p>Hi <b>{recipient}</b>,</p><p><b>{initiator}</b> proposes to swap their <b>{shiftCode}</b> on <b>{shiftDate}</b> for your <b>{otherShiftCode}</b> on <b>{otherShiftDate}</b>.</p>{msgHtml}<p style=\"margin-top:18px;\">Open OnCallendar to accept, decline or counter-offer.</p>"
                    : $"<p>Hi <b>{recipient}</b>,</p><p><b>{initiator}</b> proposes you to take their <b>{shiftCode}</b> on <b>{shiftDate}</b>.</p>{msgHtml}<p style=\"margin-top:18px;\">Open OnCallendar to accept or decline.</p>";
                return new TplStrings(
                    Title: title,
                    ShortMessage: isSwap ? $"{Get(d,"InitiatorName")} proposes a swap on {shiftDate}" : $"{Get(d,"InitiatorName")} offers you {shiftCode} on {shiftDate}",
                    EmailSubject: $"[OnCallendar] {title} from {Get(d,"InitiatorName")}",
                    EmailHeading: title,
                    EmailBodyHtml: body,
                    CtaLabel: "Open request",
                    PushTitle: title,
                    PushBody: isSwap ? $"{Get(d,"InitiatorName")} → swap {shiftCode} on {shiftDate}" : $"{Get(d,"InitiatorName")} → {shiftCode} on {shiftDate}");
            }

            case NotificationTypeCodes.SwapAccepted:
                return new TplStrings(
                    Title: "Request accepted",
                    ShortMessage: $"{Get(d,"CounterpartName")} accepted {shiftCode} on {shiftDate}",
                    EmailSubject: $"[OnCallendar] {Get(d,"CounterpartName")} accepted your request",
                    EmailHeading: "Request accepted ✓",
                    EmailBodyHtml: $"<p>Hi <b>{recipient}</b>,</p><p>Good news: <b>{E(Get(d,"CounterpartName"))}</b> accepted your request on shift <b>{shiftCode}</b> on <b>{shiftDate}</b>.</p><p style=\"margin-top:18px;\">The calendar has been updated.</p>",
                    CtaLabel: "Open calendar",
                    PushTitle: "Request accepted",
                    PushBody: $"{Get(d,"CounterpartName")} → {shiftCode} on {shiftDate}");

            case NotificationTypeCodes.SwapRejected:
                return new TplStrings(
                    Title: "Request declined",
                    ShortMessage: $"{Get(d,"CounterpartName")} declined the {shiftDate} shift",
                    EmailSubject: $"[OnCallendar] {Get(d,"CounterpartName")} declined your request",
                    EmailHeading: "Request declined",
                    EmailBodyHtml: $"<p>Hi <b>{recipient}</b>,</p><p><b>{E(Get(d,"CounterpartName"))}</b> declined your request on shift <b>{shiftCode}</b> on <b>{shiftDate}</b>.</p>{reasonHtml}<p style=\"margin-top:18px;\">You can offer it to another colleague or post it to the board.</p>",
                    CtaLabel: "Open OnCallendar",
                    PushTitle: "Request declined",
                    PushBody: $"{Get(d,"CounterpartName")} → {shiftCode} on {shiftDate}");

            case NotificationTypeCodes.SwapCancelled:
                return new TplStrings(
                    Title: "Request cancelled",
                    ShortMessage: $"{Get(d,"InitiatorName")} cancelled the {shiftDate} request",
                    EmailSubject: $"[OnCallendar] Request cancelled by {Get(d,"InitiatorName")}",
                    EmailHeading: "Request cancelled",
                    EmailBodyHtml: $"<p>Hi <b>{recipient}</b>,</p><p><b>{initiator}</b> cancelled the request on shift <b>{shiftCode}</b> on <b>{shiftDate}</b>.</p>",
                    CtaLabel: "Open OnCallendar",
                    PushTitle: "Request cancelled",
                    PushBody: $"{Get(d,"InitiatorName")} → {shiftCode} on {shiftDate}");

            case NotificationTypeCodes.CounterOfferReceived:
                return new TplStrings(
                    Title: "Counter-offer received",
                    ShortMessage: $"{Get(d,"CounterpartName")} offers {otherShiftCode} on {otherShiftDate}",
                    EmailSubject: $"[OnCallendar] Counter-offer from {Get(d,"CounterpartName")}",
                    EmailHeading: "Counter-offer received",
                    EmailBodyHtml: $"<p>Hi <b>{recipient}</b>,</p><p><b>{E(Get(d,"CounterpartName"))}</b> offers to take your <b>{shiftCode}</b> on <b>{shiftDate}</b> in exchange for their <b>{otherShiftCode}</b> on <b>{otherShiftDate}</b>.</p>{msgHtml}",
                    CtaLabel: "View counter-offer",
                    PushTitle: "Counter-offer received",
                    PushBody: $"{Get(d,"CounterpartName")} → {otherShiftCode} on {otherShiftDate}");

            case NotificationTypeCodes.CounterOfferAccepted:
                return new TplStrings(
                    Title: "Counter-offer accepted",
                    ShortMessage: $"Your counter-offer on {shiftDate} was accepted",
                    EmailSubject: "[OnCallendar] Counter-offer accepted",
                    EmailHeading: "Counter-offer accepted ✓",
                    EmailBodyHtml: $"<p>Hi <b>{recipient}</b>,</p><p>Your counter-offer on shift <b>{shiftCode}</b> on <b>{shiftDate}</b> was <b>accepted</b>. Shifts have been swapped.</p>",
                    CtaLabel: "Open calendar",
                    PushTitle: "Counter-offer accepted",
                    PushBody: $"{shiftCode} on {shiftDate}");

            case NotificationTypeCodes.CounterOfferRejected:
                return new TplStrings(
                    Title: "Counter-offer declined",
                    ShortMessage: $"Your counter-offer on {shiftDate} was declined",
                    EmailSubject: "[OnCallendar] Counter-offer declined",
                    EmailHeading: "Counter-offer declined",
                    EmailBodyHtml: $"<p>Hi <b>{recipient}</b>,</p><p>Your counter-offer on shift <b>{shiftCode}</b> on <b>{shiftDate}</b> was declined.</p>{reasonHtml}",
                    CtaLabel: "Open OnCallendar",
                    PushTitle: "Counter-offer declined",
                    PushBody: $"{shiftCode} on {shiftDate}");

            case NotificationTypeCodes.ShiftAssigned:
                return new TplStrings(
                    Title: "New shift assigned",
                    ShortMessage: $"You were assigned shift {shiftCode} on {shiftDate}",
                    EmailSubject: $"[OnCallendar] New shift: {shiftCode} on {shiftDate}",
                    EmailHeading: "New shift assigned",
                    EmailBodyHtml: $"<p>Hi <b>{recipient}</b>,</p><p>You were assigned a new shift: <b>{shiftCode}</b> on <b>{shiftDate}</b>.</p>",
                    CtaLabel: "Open calendar",
                    PushTitle: "New shift",
                    PushBody: $"{shiftCode} on {shiftDate}");

            case NotificationTypeCodes.ShiftReassigned:
                return new TplStrings(
                    Title: "Shift updated",
                    ShortMessage: $"Your shift {shiftCode} on {shiftDate} was updated",
                    EmailSubject: $"[OnCallendar] Shift updated: {shiftCode} on {shiftDate}",
                    EmailHeading: "Shift updated",
                    EmailBodyHtml: $"<p>Hi <b>{recipient}</b>,</p><p>Your shift <b>{shiftCode}</b> on <b>{shiftDate}</b> was updated.</p>",
                    CtaLabel: "Open calendar",
                    PushTitle: "Shift updated",
                    PushBody: $"{shiftCode} on {shiftDate}");

            case NotificationTypeCodes.ShiftRemoved:
                return new TplStrings(
                    Title: "Shift removed",
                    ShortMessage: $"Your shift {shiftCode} on {shiftDate} was removed",
                    EmailSubject: $"[OnCallendar] Shift removed: {shiftCode} on {shiftDate}",
                    EmailHeading: "Shift removed",
                    EmailBodyHtml: $"<p>Hi <b>{recipient}</b>,</p><p>Your shift <b>{shiftCode}</b> on <b>{shiftDate}</b> was removed from the calendar.</p>",
                    CtaLabel: "Open calendar",
                    PushTitle: "Shift removed",
                    PushBody: $"{shiftCode} on {shiftDate}");

            case NotificationTypeCodes.ShiftPostedToBoard:
                return new TplStrings(
                    Title: "Shift available on board",
                    ShortMessage: $"Available on board: {shiftCode} on {shiftDate}",
                    EmailSubject: $"[OnCallendar] Shift on board: {shiftCode} on {shiftDate}",
                    EmailHeading: "New shift on the board",
                    EmailBodyHtml: $"<p>Hi <b>{recipient}</b>,</p><p><b>{initiator}</b> posted shift <b>{shiftCode}</b> on <b>{shiftDate}</b> to the board: who'll take it?</p>",
                    CtaLabel: "Open board",
                    PushTitle: "Shift on board",
                    PushBody: $"{shiftCode} on {shiftDate}");

            case NotificationTypeCodes.ExternalDoctorAssigned:
                return new TplStrings(
                    Title: "Shift handed over to external doctor",
                    ShortMessage: $"Your shift {shiftCode} on {shiftDate} was handed over to an external doctor",
                    EmailSubject: "[OnCallendar] Shift handed over to external doctor",
                    EmailHeading: "Shift handed over to external doctor",
                    EmailBodyHtml: $"<p>Hi <b>{recipient}</b>,</p><p>Your shift <b>{shiftCode}</b> on <b>{shiftDate}</b> was handed over to <b>{E(Get(d,"ExternalDoctorName"))}</b>.</p>",
                    CtaLabel: "Open calendar",
                    PushTitle: "Shift handed over",
                    PushBody: $"{shiftCode} on {shiftDate} → {Get(d,"ExternalDoctorName")}");

            case NotificationTypeCodes.ReminderShiftTomorrow:
                return new TplStrings(
                    Title: "Shift reminder",
                    ShortMessage: $"Tomorrow you're on duty: {shiftCode} on {shiftDate}",
                    EmailSubject: "[OnCallendar] Reminder: tomorrow you're on duty",
                    EmailHeading: "Tomorrow you're on duty",
                    EmailBodyHtml: $"<p>Hi <b>{recipient}</b>,</p><p>Reminder: tomorrow <b>{shiftDate}</b> you're on duty (<b>{shiftCode}</b>).</p>",
                    CtaLabel: "Open calendar",
                    PushTitle: "Tomorrow on duty",
                    PushBody: $"{shiftCode} on {shiftDate}");

            case NotificationTypeCodes.ReminderOnCallToday:
                return new TplStrings(
                    Title: "On-call reminder",
                    ShortMessage: $"Today you're on call ({shiftDate})",
                    EmailSubject: "[OnCallendar] Today you're on call",
                    EmailHeading: "Today you're on call",
                    EmailBodyHtml: $"<p>Hi <b>{recipient}</b>,</p><p>Reminder: today <b>{shiftDate}</b> you're on call.</p>",
                    CtaLabel: "Open calendar",
                    PushTitle: "On call today",
                    PushBody: shiftDate);

            case NotificationTypeCodes.SystemAnnouncement:
            default:
                return new TplStrings(
                    Title: Get(d, "Title", "Announcement"),
                    ShortMessage: Get(d, "Body", string.Empty),
                    EmailSubject: $"[OnCallendar] {Get(d, "Title", "Announcement")}",
                    EmailHeading: Get(d, "Title", "Announcement"),
                    EmailBodyHtml: $"<p>{E(Get(d, "Body"))}</p>",
                    CtaLabel: "Open OnCallendar",
                    PushTitle: Get(d, "Title", "OnCallendar"),
                    PushBody: Get(d, "Body", string.Empty));
        }
    }

    private static string HtmlToText(string html)
    {
        if (string.IsNullOrEmpty(html)) return string.Empty;
        var noTags = System.Text.RegularExpressions.Regex.Replace(html, "<.*?>", string.Empty);
        return WebUtility.HtmlDecode(noTags).Trim();
    }
}
