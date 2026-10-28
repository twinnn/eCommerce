# Agents act through Proposals, with autonomy granted per action type

The end goal is fully autonomous Agents, but every write to an outside system (Pohoda, a carrier, a social network) starts as a Proposal that a person approves; a person later grants autonomy one action type at a time (e.g. "publish post", "create Invoice") once that action has a clean track record of approvals without edits. Every autonomous action stays logged. We chose this over building autonomous from day one because the first live Pohoda writes are still unverified, and over permanent human approval because the point of the system is to take work off the Distributor.

## Consequences

- Every Agent's output is shaped as a Proposal that can be previewed, approved, edited or rejected, so one shared approval surface serves all Agents.
- Autonomy is configuration per action type, not a property of the Agent as a whole.
