"""Publication receipt boundaries; synthetic fixtures are not GPU evidence."""
import copy
import importlib.util
from pathlib import Path
import unittest

spec = importlib.util.spec_from_file_location('gate', Path(__file__).with_name('runtime-distribution-release.py'))
gate = importlib.util.module_from_spec(spec); spec.loader.exec_module(gate)


class ReceiptTests(unittest.TestCase):
    def setUp(self):
        self.commit='a'*40; self.sha='b'*64
        self.receipt=dict(completed=True,sourceTreeDirty=False,distributionRuntime=True,interopBoundaries=True,
            extendedLinux=False,validationCommit=self.commit,sourceCommit=gate.CORE_COMMIT,
            corePackageSha256=gate.CORE_SHA,runtimePackageSha256=self.sha,platform='win-x64',host='windows',
            workloads=[dict(workload=n,status='passed',exitCode=1 if n=='MissingHipRtcRejected' else 0)
                       for n in sorted(gate.core.WORKLOADS | {'AdvancedInteropBoundaries','FrameworkPackage'})])

    def check(self, receipt=None, platform='win-x64'):
        gate.receipt_check(receipt or self.receipt,platform,self.sha,self.commit)

    def test_synthetic_full_windows_receipt(self): self.check()

    def test_incomplete_receipt_rejected(self):
        self.receipt['completed']=False
        with self.assertRaisesRegex(ValueError,'Incomplete'):self.check()

    def test_dirty_receipt_rejected(self):
        self.receipt['sourceTreeDirty']=True
        with self.assertRaisesRegex(ValueError,'Incomplete'):self.check()

    def test_old_local_format_receipt_rejected(self):
        self.receipt['distributionRuntime']=False
        with self.assertRaisesRegex(ValueError,'Incomplete'):self.check()

    def test_missing_interop_receipt_rejected(self):
        self.receipt['interopBoundaries']=False
        with self.assertRaisesRegex(ValueError,'Incomplete'):self.check()

    def test_each_source_and_package_binding_rejected(self):
        for field in ('validationCommit','sourceCommit','corePackageSha256','runtimePackageSha256'):
            receipt=copy.deepcopy(self.receipt);receipt[field]='f'*64
            with self.subTest(field=field),self.assertRaisesRegex(ValueError,'source or exact'):self.check(receipt)

    def test_wrong_host_rejected(self):
        self.receipt['host']='wsl2'
        with self.assertRaisesRegex(ValueError,'host'):self.check()

    def test_missing_workload_rejected(self):
        self.receipt['workloads'].pop()
        with self.assertRaisesRegex(ValueError,'Missing or duplicated'):self.check()

    def test_duplicate_cannot_replace_required_workload(self):
        self.receipt['workloads'][0]=self.receipt['workloads'][1]
        with self.assertRaisesRegex(ValueError,'Missing or duplicated'):self.check()

    def test_gpu_failure_rejected(self):
        self.receipt['workloads'][0]['status']='failed'
        with self.assertRaisesRegex(ValueError,'Failed deployment'):self.check()

    def test_missing_hiprtc_must_fail_natively(self):
        next(r for r in self.receipt['workloads'] if r['workload']=='MissingHipRtcRejected')['exitCode']=0
        with self.assertRaisesRegex(ValueError,'Failed deployment'):self.check()

    def test_unapproved_skip_rejected(self):
        self.receipt['workloads'][0]['status']='skipped-capability'
        with self.assertRaisesRegex(ValueError,'Unapproved capability'):self.check()

    def test_only_documented_capabilities_may_skip(self):
        next(r for r in self.receipt['workloads'] if r['workload']=='ManagedMemory')['status']='skipped-capability'
        self.check()

    def test_linux_receipt_has_no_windows_framework_claim(self):
        self.receipt.update(platform='linux-x64',host='wsl2')
        self.receipt['workloads']=[r for r in self.receipt['workloads'] if r['workload']!='FrameworkPackage']
        for row in self.receipt['workloads']:
            if row['workload'] in ('ManagedMemory','ArrayTextureSurface'):row['status']='skipped-capability'
        self.check(platform='ubuntu.24.04-x64')


if __name__=='__main__':unittest.main(verbosity=2)
