# Enterprise Contact Events Project Goal

```mermaid
flowchart LR
    subgraph Producers["Event Producers"]
        CRM["CRM / MDM Platform"]
        ProductSys["Product & Holding Systems"]
    end

    subgraph Messaging["Enterprise Messaging Backbone"]
        SB["Azure Service Bus Namespace"]
        ContactTopic["contact.events Topic"]
    end

    subgraph Consumers["Event Consumers"]
        Digital["Digital Channels<br/>(Cache / Read Models)"]
        Insurance["Insurance Business Unit"]
        Parks["Parks & Resorts Business Unit"]
        Carwash["Carwash"]
    end

    CRM -->|"ContactUpdated Event"| ContactTopic
    ProductSys -->|"Product / Holding Changes"| ContactTopic

    SB --> ContactTopic

    ContactTopic -->|"All Contact Events"| Digital
    ContactTopic -->|"Filtered: hasInsurance = true"| Insurance
    ContactTopic -->|"Filtered: hasParksResorts = true"| Parks
    ContactTopic -->|"Filtered: hasCarwashProduct = true"| Carwash

    Carwash --> Pulse["Pulse API - Contact CRUD"]
```

This diagram is the canonical project goal. Azure Service Bus provides the
`contact.events` topic as the enterprise messaging backbone for contact, product, and
holding changes. Consumers receive either all contact events or subscription-filtered
events based on their business capability. Carwash uses matching events to integrate
with the Pulse Contact CRUD API.
