# STOP Conditions (Human Decision Required)

Stop and ask for an ADR if any of these decisions are required:

1) Changing core tech stack (db/broker/observability)
2) Introducing new user/account/auth flows
3) Adding recommendation ranking / ML
4) Multi-region / geo replication requirements
5) Strong consistency guarantees beyond the defined NFRs
6) Deleting/editing posts with strict propagation semantics
7) Any new feature that is not listed in `docs/agent/SCOPE.md`
