# Musiq Architecture

Musiq is split into Domain, Application, Infrastructure, Presentation, and App composition root layers.

- Domain: business entities and rules only.
- Application: use cases and abstractions.
- Infrastructure: SQLite, metadata, audio, filesystem and external integrations.
- Presentation: WPF/MVVM UI.
- App: startup, DI and lifetime.

Dependencies point inward; UI never talks directly to database, audio engine or metadata libraries.
