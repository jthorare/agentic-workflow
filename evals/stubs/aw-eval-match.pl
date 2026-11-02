#!/usr/bin/perl
# Canned-answer matcher for the eval stubs. Usage: aw-eval-match.pl gh|curl FILE -- ARGS...
# Prints the first matching entry's stdout/stderr and exits with its exit code. Exit 250: no entry matches.
use strict; use warnings; use JSON::PP; $| = 1;
my ($mode, $file) = (shift, shift); shift;    # the "--"
my @call = @ARGV;
open(my $fh, "<", $file) or do { print STDERR "aw-eval: cannot read $file\n"; exit 1 };
my $entries = eval { decode_json(do { local $/; <$fh> }) };
if (ref $entries ne "ARRAY") { print STDERR "aw-eval: $file is not a JSON array\n"; exit 1 }

# flags: ["--name", value|undef] pairs; "--name=value", or "--name value" when the next item is not a flag
sub parse_flags {
  my @a = @_; my @f;
  for (my $i = 0; $i < @a; $i++) {
    next unless $a[$i] =~ /^-/;
    if ($a[$i] =~ /^(--[^=]+)=(.*)$/s) { push @f, [$1, $2] }
    elsif ($i + 1 < @a && $a[$i + 1] !~ /^-/) { push @f, [$a[$i], $a[$i + 1]]; $i++ }
    else { push @f, [$a[$i], undef] }
  }
  return @f;
}
sub set { join ",", sort split /,/, $_[0] }
sub emit { my $e = shift; print STDOUT ($e->{stdout} // ""); print STDERR ($e->{stderr} // ""); exit(($e->{exit} // 0) + 0) }

if ($mode eq "gh") {
  my @args; # strip -R X / --repo X / --repo=X wherever they sit
  for (my $i = 0; $i < @call; $i++) {
    if ($call[$i] eq "-R" || $call[$i] eq "--repo") { $i++; next }
    next if $call[$i] =~ /^--repo=/ || $call[$i] =~ /^-R./;
    push @args, $call[$i];
  }
  my @lead; for my $x (@args) { last if $x =~ /^-/; push @lead, $x }
  my @cf = parse_flags(@args[@lead .. $#args]);
  for my $e (@$entries) {
    my @m = @{ $e->{match} // [] };
    my @ml; for my $x (@m) { last if $x =~ /^-/; push @ml, $x }
    next unless join("\0", @ml) eq join("\0", @lead);
    my $ok = 1;
    for my $mf (parse_flags(@m[@ml .. $#m])) {
      my $hit = 0;
      for my $c (@cf) {
        next unless $c->[0] eq $mf->[0];
        if (!defined $mf->[1] || (defined $c->[1] && set($c->[1]) eq set($mf->[1]))) { $hit = 1; last }
      }
      if (!$hit) { $ok = 0; last }
    }
    emit($e) if $ok;
  }
  exit 250;
}

if ($mode eq "curl") {
  my ($method, $url, $data, $head) = ("", "", 0, 0);
  for (my $i = 0; $i < @call; $i++) {
    my $a = $call[$i];
    if ($a eq "-X" || $a eq "--request") { $method = $call[++$i] // "" }
    elsif ($a =~ /^--request=(.*)$/ || $a =~ /^-X(.+)$/) { $method = $1 }
    elsif ($a =~ /^(-d|--data.*|-F|--form.*)$/ || $a =~ /^-[dF]./) { $data = 1; $i++ if $a =~ /^(-d|--data.*|-F|--form.*)$/ && $a !~ /=/ }
    elsif ($a eq "-I" || $a eq "--head") { $head = 1 }
    elsif ($a =~ m{^https?://}) { $url = $a }
  }
  $method = uc($method || ($data ? "POST" : $head ? "HEAD" : "GET"));
  for my $e (@$entries) {
    my @m = @{ $e->{match} // [] };
    my ($mm, $mu) = @m >= 2 ? (uc $m[0], $m[1]) : ("GET", $m[0] // "");
    next unless $mm eq $method && length $mu && index($url, $mu) == 0;
    emit($e);
  }
  print STDERR "aw-eval: curl $method $url has no canned answer\n";
  exit 7;
}
exit 2;
