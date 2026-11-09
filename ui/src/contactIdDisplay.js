export const SHORT_CONTACT_ID_LENGTH = 7;

export function shortContactId(contactId) {
  if (typeof contactId !== "string") return "";
  return contactId.slice(0, SHORT_CONTACT_ID_LENGTH);
}
