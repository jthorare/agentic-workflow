export function work(job: Job) {
  const n = job.attempts;
  if (n > 3) return;
  run(job);
  log(job);
}

function run(job: Job) {}
function log(job: Job) {}
type Job = { attempts: number };
export default work;
