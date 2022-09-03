use sha3::{Digest, Sha3_384};

pub fn hash(input: &str) -> String {
    let mut hasher = Sha3_384::default();
    hasher.update(input.as_bytes());
    let result = hasher.finalize();
    base64::encode(result)
}
