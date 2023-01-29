use base64::Engine;
use base64::prelude::BASE64_STANDARD;
use sha3::{Digest, Sha3_384};

// TerritoryIntendedUse = 13 or 14
pub const HOUSING_ZONES: &[u32] = &[
    282,
    283,
    284,
    339,
    340,
    341,
    342,
    343,
    344,
    345,
    346,
    347,
    384,
    385,
    386,
    423,
    424,
    425,
    573,
    574,
    575,
    608,
    609,
    610,
    641,
    649,
    650,
    651,
    652,
    653,
    654,
    655,
    979,
    980,
    981,
    982,
    983,
    984,
    985,
    999,
];

pub fn hash(input: &str) -> String {
    let mut hasher = Sha3_384::default();
    hasher.update(input.as_bytes());
    let result = hasher.finalize();
    BASE64_STANDARD.encode(result)
}
