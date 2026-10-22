Recorded on macOS 26 (arm64) and scrubbed: the home directory is /Users/dev, every other path is a system or tmux path.

- ps.txt: `TZ=UTC LC_ALL=C ps -axww -o pid=,ppid=,uid=,stat=,tty=,lstart=,comm=`, a dozen rows picked by hand (the root rows and the
  `SentinelOne Extensions` row show another user's process and a comm with spaces). Row 759's stat was edited from S to Z to make a zombie.
- lsof-files.txt: `lsof -a -p <tmux server> -d 0-65535 -F ftdn` (open file descriptors only).
- lsof-cwd.txt: `lsof -a -p <pid> -d cwd -F n`.
- lsof-sockets.txt: `lsof -a -U -u <uid> -F pftdn`, cut to the tmux server's first three sockets and one connected (path-less) socket of another process.
