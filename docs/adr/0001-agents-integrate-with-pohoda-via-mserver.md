# Agents integrate with Pohoda through mServer, not NextCom customizations

Pohoda already has NextCom customizations that turn an Order into an Invoice and an Invoice into a shipping label at the press of a button. Agents will not drive those buttons. They read and write Pohoda through our own mServer client (`Pohoda.MServer`) and call the carriers (SPS, Packeta) through their APIs directly, because the NextCom actions are not known to be callable outside the Pohoda UI, and owning the integration keeps the Agents testable and portable to other businesses later.

## Consequences

- Business rules NextCom encoded in those buttons (numbering, texts, carrier choice) must be rediscovered and reimplemented, not assumed.
- The product-sync flag NextCom reads (hide from e-shop) is still respected: Agents set it in Pohoda and let NextCom's sync carry it to Go-Noow.
