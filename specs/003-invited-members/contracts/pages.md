# Contract: Invitation and session pages

The stored shape is [data-model.md](../data-model.md). This contract is what a signed-in person sees. Another group's invitations, members, sessions, votes, and pick counts are never on the page. A signed-out request for a session asks the person to sign in and shows no session content. An account that is not an accepted member of that group gets not found and no session content.

## Home (`/groups`)

Pending invitations for the signed-in account's email are listed above that account's groups. Each row shows the group name and the inviter's display name in that group. It does not show that group's sessions, votes, members, or pick counts.

Accept asks for a display name of 1 to 40 characters. A blank name, a longer name, or a name already used in the group leaves the invitation pending and shows that the name was not accepted. When the group is already at 8 active account members, accept leaves the invitation pending and shows that the group is full. A successful accept opens that group.

Decline removes the invitation from the home. The person is not a member.

If the signed-in account has any pending invitation, sign-in lands on `/groups`. Otherwise sign-in keeps today's landing: one group opens the catalog, more than one opens `/groups`, and none opens the new-group page.

This page lists an invitation only when its email is the signed-in account's email. It does not list an invitation that account sent, and it has no cancel action.

## Members

The list is active memberships with an account. Name-only seats are not listed and cannot be selected.

Any accepted member may submit an email address. Success adds a pending invitation and does not send a message. The form refuses a blank email or an email `Account.EmailAccepted` rejects, the inviter's own email, an email that already belongs to an active member, an email that already has a pending invitation for this group, and an invitation that would make the cap exceed 8.

Pending invitations are listed on this page. The member who created one sees that row, including the email, and may cancel it. The group creator sees every pending invitation in the group, including each email, and may cancel any of them. Another member sees neither the row nor the email.

The group creator may remove another member. A member who is not the creator sees Leave on their own row and does not see Remove on anyone else. The creator has no Leave control. Leave and remove do the same thing to access: the person no longer sees the group. Past sessions still show them.

The group creator may rename an active account membership. That is the existing rename, and it is not a way to add a person. A name-only seat is not listed, so it has no rename control.

## Session

A session page shows the topics its creator chose. Ordinary members do not see a control to change those topics.

Before a facilitator is elected, the session creator may replace the 2 or 3 topics. One topic, four or more, a duplicate, or a worked design changes nothing and does not change pick counts. After that session elects a facilitator, the topic list stays.

Each accepted member sees present / not present for themselves only. Submitting another member's id changes nothing.

Each member in the voting set who has not voted sees their own vote form. The candidates are the voting set. A set of two may include themselves. A larger set does not. Submitting a vote for someone else changes nothing.

When a member leaves or is removed after the voting set exists and they have not voted, the page no longer waits for their vote. Votes already cast stay, including a vote for them. If at least two voters remain and all of them have voted, the facilitator is shown. If fewer than two voters remain, no facilitator is shown. A member who already voted and then leaves still counts as a voter. They get not found if they open the session afterward.

## Creating a session

The group creator, and the facilitator of the highest-numbered session that already has a facilitator, see a create form that requires 2 or 3 topics. Other members do not see it, and a direct submit is refused with no session and no pick-count change.

The first session can be created only by the group creator, with its topics. A session is never stored without those topics.

The create form is shown only when the group has no session waiting for an election. While that waiting session exists, the creator and the current facilitator do not see the form, and a direct submit writes no session and no pick-count change.

The facilitator elected on the session being viewed uses that form to plan the following session. They do not get a control to replace the current session's topics unless they are also that session's creator and it has not elected yet.

## Refusals

A refused invite, accept, decline, cancel, leave, remove, attendance mark, vote, or session create shows the same reference as the stored activity row. The page does not show an email address, a display name from another group, or a stack dump.

Attempt names added for this feature: `invite-member`, `cancel-invitation`, `accept-invitation`, `decline-invitation`, `leave-group`, `mark-attendance`. The list-shaped `save-attendance` attempt is no longer recorded.
