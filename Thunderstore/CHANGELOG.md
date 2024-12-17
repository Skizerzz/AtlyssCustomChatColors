1.0.2:
- Initial upload


1.0.4:
- Added "/chatcolor clear" command to remove chat coloring from self.
- Reduced memory footprint such that only data pertinent to those in the lobby is in memory.
- Fixed a bug where having "/chatcolor" within a message anywhere besides the beginning would cause your message to not have color applied.
- Chat color settings are now only saved to the database upon client or server disconnect.
- Chat color settings are now only loaded from the database when a client first sends a chat message.

1.0.5:
- Updated for Atlyss patch 1.6.0a