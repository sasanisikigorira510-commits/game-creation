"""Explicit Sandbox-only IAP assembly; the existing non-IAP QA guard is retained.

No deployment, schema migration, gate renewal, notification registration or
purchase occurs during construction. Production paths never become accessible.
"""
import json
import os
import re
import uuid
from pathlib import Path

from apple_application import create_apple_app
from apple_revocation_runtime import private, read_private, require, unique
from apple_verifier import build_verifier
from qa_gateway import QaGateway
from qa_isolated_runtime import ROOT, guard, NoDeletion

CONFIG = ROOT / 'iap-sandbox.json'
CREDENTIALS = Path('/etc/nasus-iap-sandbox-20260928')
BUNDLE = 'com.nasus.dungeonmonsterroguelike'
FIELDS = {'Version', 'Environment', 'BundleId', 'KeyId', 'IssuerId'}


def purchase_environment(environ):
    # Keep every DB, isolation marker and backup check of the original runtime.
    guard(environ)
    config = json.loads(read_private(CONFIG, 4096), object_pairs_hook=unique)
    require(isinstance(config, dict) and set(config) == FIELDS)
    require(type(config['Version']) is int and config['Version'] == 1)
    require(config['Environment'] == 'Sandbox' and config['BundleId'] == BUNDLE)
    require(isinstance(config['KeyId'], str) and re.fullmatch('[A-Z0-9]{10}', config['KeyId']))
    require(isinstance(config['IssuerId'], str)
            and str(uuid.UUID(config['IssuerId'])) == config['IssuerId'])
    private(CREDENTIALS, True)
    signing = private(CREDENTIALS / 'signing.p8')
    roots = private(CREDENTIALS / 'roots', True)
    require(bool(list(roots.glob('*.cer'))))
    for certificate in roots.glob('*.cer'): private(certificate)
    return dict(environ, WITCH_APPLE_ENVIRONMENT='Sandbox', WITCH_APPLE_BUNDLE_ID=BUNDLE,
                WITCH_APPLE_KEY_ID=config['KeyId'], WITCH_APPLE_ISSUER_ID=config['IssuerId'],
                WITCH_APPLE_KEY_FILE=str(signing), WITCH_APPLE_ROOTS_DIR=str(roots))


def application_factory():
    os.umask(0o077)
    environ = purchase_environment(os.environ)
    # Validate the unchanged expiring gate before opening a DB or loading IAP.
    gate = QaGateway(None, json.loads(read_private(environ['WITCH_QA_GATE_CONFIG'], 4096),
                                     object_pairs_hook=unique))
    verifier = build_verifier(environ, sandbox_only=True)
    gate.app = NoDeletion(create_apple_app(environ, purchase_verifier=verifier))
    return gate
